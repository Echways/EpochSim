using System.Collections.Generic;
using EpochSim.Kernel.Time;
using EpochSim.Kernel.Messaging;

namespace EpochSim.Kernel.Scheduling;

public sealed class Scheduler : IScheduler
{
    private long _sequence = 0;
    private readonly SortedSet<ScheduledItem> _queue = new(new ScheduledItemComparer());

    public void Schedule(SimTime time, IEvent ev)
        => _queue.Add(new ScheduledItem(time, ++_sequence, ev));

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
            var t = a.Time.Tick.CompareTo(b.Time.Tick);
            if (t != 0) return t;
            return a.Seq.CompareTo(b.Seq);
        }
    }
}
