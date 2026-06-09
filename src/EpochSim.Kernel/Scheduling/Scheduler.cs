using System;
using System.Collections.Generic;
using EpochSim.Kernel.Messaging;
using EpochSim.Kernel.Time;

namespace EpochSim.Kernel.Scheduling;

public sealed class Scheduler : IScheduler
{
    private long _sequence = 0;
    private readonly PriorityQueue<ScheduledItem, ScheduledPriority> _queue =
        new(new ScheduledPriorityComparer());
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

        var seq = ++_sequence;
        _queue.Enqueue(new ScheduledItem(time, seq, ev), new ScheduledPriority(time.Tick, seq));
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
        if (_queue.TryDequeue(out item, out _))
            return true;

        item = default;
        return false;
    }

    public SimTime? PeekTime()
    {
        if (_queue.TryPeek(out var item, out _))
            return item.Time;

        return null;
    }

    private readonly record struct ScheduledPriority(long Tick, long Seq);

    private sealed class ScheduledPriorityComparer : IComparer<ScheduledPriority>
    {
        public int Compare(ScheduledPriority a, ScheduledPriority b)
        {
            var timeComparison = a.Tick.CompareTo(b.Tick);
            if (timeComparison != 0) return timeComparison;
            return a.Seq.CompareTo(b.Seq);
        }
    }
}
