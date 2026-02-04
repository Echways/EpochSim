using System;
using System.Collections.Generic;
using EpochSim.Kernel.Time;
using EpochSim.Kernel.Messaging;

namespace EpochSim.Kernel.Scheduling;

public sealed class Scheduler : IScheduler
{
    private long _sequence = 0;
    private readonly SortedSet<ScheduledItem> _queue = new(new ScheduledItemComparer());
    private readonly Func<SimTime> _currentTimeProvider;

    public Scheduler(Func<SimTime> currentTimeProvider)
    {
        _currentTimeProvider = currentTimeProvider ?? throw new ArgumentNullException(nameof(currentTimeProvider));
    }

    public void ScheduleAt(SimTime time, IEvent ev)
    {
        var current = _currentTimeProvider();
        if (time.Tick <= current.Tick)
            throw new InvalidOperationException($"ScheduleAt requires targetTick > currentTick (target={time.Tick}, current={current.Tick}).");

        _queue.Add(new ScheduledItem(time, ++_sequence, ev));
    }

    public void ScheduleNextTick(IEvent ev) => ScheduleInTicks(1, ev);

    public void ScheduleInTicks(long deltaTicks, IEvent ev)
    {
        if (deltaTicks < 1)
            throw new InvalidOperationException($"ScheduleInTicks requires deltaTicks >= 1 (deltaTicks={deltaTicks}).");

        var current = _currentTimeProvider();
        var target = current.AddTicks(deltaTicks);
        ScheduleAt(target, ev);
    }

    public bool TryDequeue(out ScheduledItem item)
    {
        if (_queue.Count == 0) { item = default; return false; }
        item = _queue.Min!;
        _queue.Remove(item);
        return true;
    }

    public SimTime? PeekTime() => _queue.Count == 0 ? null : _queue.Min!.Time;

    private sealed class ScheduledItemComparer : IComparer<ScheduledItem>
    {
        public int Compare(ScheduledItem a, ScheduledItem b)
        {
            var timeComparison = a.Time.Tick.CompareTo(b.Time.Tick);
            if (timeComparison != 0) return timeComparison;
            return a.Seq.CompareTo(b.Seq);
        }
    }
}
