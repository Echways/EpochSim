namespace EpochSim.Serialization.EventLog;

public sealed class EventLogIndex
{
    private readonly Dictionary<long, List<EventLogEntryV2>> _byTick = new();

    public EventLogIndex(IEnumerable<EventLogEntryV2> entries)
    {
        foreach (var e in entries)
        {
            if (!_byTick.TryGetValue(e.Tick, out var list))
            {
                list = new List<EventLogEntryV2>();
                _byTick[e.Tick] = list;
            }

            list.Add(e);
        }
    }

    public IReadOnlyList<EventLogEntryV2> GetAtTick(long tick)
        => _byTick.TryGetValue(tick, out var list) ? list : Array.Empty<EventLogEntryV2>();
}
