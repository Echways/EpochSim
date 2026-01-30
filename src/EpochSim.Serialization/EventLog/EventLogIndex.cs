namespace EpochSim.Serialization.EventLog;

public sealed class EventLogIndex
{
    private readonly Dictionary<long, List<EventLogEntry>> _byTick = new();

    public EventLogIndex(IEnumerable<EventLogEntry> entries)
    {
        foreach (var e in entries)
        {
            if (!_byTick.TryGetValue(e.Tick, out var list))
            {
                list = [];
                _byTick[e.Tick] = list;
            }

            list.Add(e);
        }
    }

    public IReadOnlyList<EventLogEntry> GetAtTick(long tick)
        => _byTick.TryGetValue(tick, out var list) ? list : Array.Empty<EventLogEntry>();
}
