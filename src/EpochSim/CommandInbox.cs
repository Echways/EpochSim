using EpochSim.Kernel.Messaging;
using EpochSim.Kernel.Time;

namespace EpochSim;

public sealed class CommandInbox
{
    private readonly object _gate = new();
    private readonly Queue<ICommand> _queue = new();

    public int Count
    {
        get
        {
            lock (_gate)
                return _queue.Count;
        }
    }

    public void Enqueue(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        lock (_gate)
            _queue.Enqueue(command);
    }

    public int DrainTo(ICommandBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        lock (_gate)
        {
            var count = 0;
            while (_queue.Count > 0)
            {
                buffer.Enqueue(_queue.Dequeue());
                count++;
            }

            return count;
        }
    }

    public IReadOnlyList<ICommand> Drain()
    {
        lock (_gate)
        {
            if (_queue.Count == 0)
                return Array.Empty<ICommand>();

            var drained = new ICommand[_queue.Count];
            for (var i = 0; i < drained.Length; i++)
                drained[i] = _queue.Dequeue();

            return drained;
        }
    }
}

public sealed class ScheduledCommandInbox
{
    private readonly object _gate = new();
    private readonly PriorityQueue<ICommand, ScheduledPriority> _queue = new();
    private long _nextSequence;

    public int Count
    {
        get
        {
            lock (_gate)
                return _queue.Count;
        }
    }

    public void Enqueue(SimTime at, ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        lock (_gate)
        {
            if (_nextSequence == long.MaxValue)
                throw new InvalidOperationException("ScheduledCommandInbox sequence overflow.");

            _queue.Enqueue(command, new ScheduledPriority(at.Tick, _nextSequence));
            _nextSequence++;
        }
    }

    public void Enqueue(long tick, ICommand command)
        => Enqueue(new SimTime(tick), command);

    public int DrainAt(SimTime now, ICommandBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        lock (_gate)
        {
            var drained = 0;

            while (_queue.TryPeek(out _, out var priority) && priority.Tick <= now.Tick)
            {
                var command = _queue.Dequeue();
                buffer.Enqueue(command);
                drained++;
            }

            return drained;
        }
    }

    private readonly record struct ScheduledPriority(long Tick, long Sequence) : IComparable<ScheduledPriority>
    {
        public int CompareTo(ScheduledPriority other)
        {
            var tickCompare = Tick.CompareTo(other.Tick);
            if (tickCompare != 0)
                return tickCompare;

            return Sequence.CompareTo(other.Sequence);
        }
    }
}
