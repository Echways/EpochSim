using EpochSim.Serialization.EventLog;

namespace EpochSim.Execution.Diagnostics;

public sealed record EventLogStats(
    long TotalEvents,
    long MinTick,
    long MaxTick,
    IReadOnlyDictionary<string, long> ByKind,
    IReadOnlyDictionary<long, long> ByTick,
    IReadOnlyList<(long Tick, long Count)> TopTicks,
    IReadOnlyList<(string Kind, long Count)> TopKinds);

public static class EventLogStatsComputer
{
    public static EventLogStats Compute(string eventsPath, long? fromTickInclusive = null, long? toTickInclusive = null, int topN = 20)
    {
        var byKind = new Dictionary<string, long>(StringComparer.Ordinal);
        var byTick = new Dictionary<long, long>();

        long total = 0;
        long minTick = long.MaxValue;
        long maxTick = long.MinValue;

        foreach (var line in File.ReadLines(eventsPath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var tick = ReadLongField(line, "\"t\":");
            if (fromTickInclusive.HasValue && tick < fromTickInclusive.Value) continue;
            if (toTickInclusive.HasValue && tick > toTickInclusive.Value) continue;

            var kind = ReadStringField(line, "\"kind\":");

            total++;

            if (tick < minTick) minTick = tick;
            if (tick > maxTick) maxTick = tick;

            byKind.TryGetValue(kind, out var kc);
            byKind[kind] = kc + 1;

            byTick.TryGetValue(tick, out var tc);
            byTick[tick] = tc + 1;
        }

        if (total == 0)
        {
            minTick = 0;
            maxTick = 0;
        }

        var topKinds = byKind
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Take(Math.Max(1, topN))
            .Select(kv => (kv.Key, kv.Value))
            .ToArray();

        var topTicks = byTick
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key)
            .Take(Math.Max(1, topN))
            .Select(kv => (kv.Key, kv.Value))
            .ToArray();

        return new EventLogStats(
            TotalEvents: total,
            MinTick: minTick,
            MaxTick: maxTick,
            ByKind: byKind,
            ByTick: byTick,
            TopTicks: topTicks,
            TopKinds: topKinds);
    }

    private static long ReadLongField(string line, string field)
    {
        var i = line.IndexOf(field, StringComparison.Ordinal);
        if (i < 0) throw new FormatException($"Missing field {field}");

        i += field.Length;
        while (i < line.Length && char.IsWhiteSpace(line[i])) i++;

        var end = i;
        while (end < line.Length && (char.IsDigit(line[end]) || line[end] == '-')) end++;

        if (!long.TryParse(line.AsSpan(i, end - i), out var v))
            throw new FormatException($"Invalid number for {field}");

        return v;
    }

    private static string ReadStringField(string line, string field)
    {
        var i = line.IndexOf(field, StringComparison.Ordinal);
        if (i < 0) throw new FormatException($"Missing field {field}");

        i += field.Length;
        while (i < line.Length && char.IsWhiteSpace(line[i])) i++;

        if (i >= line.Length || line[i] != '"') throw new FormatException($"Invalid string for {field}");
        i++;

        var sb = new System.Text.StringBuilder();
        while (i < line.Length)
        {
            var ch = line[i++];

            if (ch == '"') break;

            if (ch == '\\')
            {
                if (i >= line.Length) throw new FormatException($"Invalid escape in {field}");
                var e = line[i++];

                sb.Append(e switch
                {
                    '"' => '"',
                    '\\' => '\\',
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    _ => e
                });

                continue;
            }

            sb.Append(ch);
        }

        return sb.ToString();
    }
}
