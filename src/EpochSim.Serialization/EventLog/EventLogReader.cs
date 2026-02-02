namespace EpochSim.Serialization.EventLog;

public static class EventLogReader
{
    public static IReadOnlyList<EventLogEntry> ReadAll(string path)
    {
        var list = new List<EventLogEntry>();

        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var tick = EventLogLine.ReadTick(line);
            var kind = EventLogLine.ReadKind(line);
            var payload = EventLogLine.ReadPayload(line);

            list.Add(new EventLogEntry(tick, kind, payload));
        }

        return list;
    }

    public static IReadOnlyList<EventLogEntry> ReadAfterTick(string path, long tickExclusive)
    {
        var all = ReadAll(path);
        return all.Where(e => e.Tick > tickExclusive).ToArray();
    }
}
