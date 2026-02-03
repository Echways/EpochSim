using System.Text.Json;
using EpochSim.Serialization.EventLog;

namespace EpochSim.Execution.Diagnostics;

public sealed record EventLogStats(
    long TotalEvents,
    long MinTick,
    long MaxTick,
    IReadOnlyDictionary<string, long> ByKind,
    IReadOnlyDictionary<long, long> ByTick,
    IReadOnlyList<(long Tick, long Count)> TopTicks,
    IReadOnlyList<(string Kind, long Count)> TopKinds,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyDictionary<int, long>>> IntFieldDistributions);

public static class EventLogStatsComputer
{
    public static EventLogStats Compute(
        string eventsPath,
        long? fromTickInclusive = null,
        long? toTickInclusive = null,
        int topN = 20,
        ISet<string>? allowedKinds = null)
    {
        var byKind = new Dictionary<string, long>(StringComparer.Ordinal);
        var byTick = new Dictionary<long, long>();
        var dist = new Dictionary<string, Dictionary<string, Dictionary<int, long>>>(StringComparer.Ordinal);

        long total = 0;
        long minTick = long.MaxValue;
        long maxTick = long.MinValue;

        foreach (var entry in EventLogReader.ReadStream(eventsPath))
        {
            var tick = entry.Tick;
            if (fromTickInclusive.HasValue && tick < fromTickInclusive.Value) continue;
            if (toTickInclusive.HasValue && tick > toTickInclusive.Value) break;

            var kind = entry.Kind;
            if (allowedKinds is not null && allowedKinds.Count > 0 && !allowedKinds.Contains(kind))
                continue;

            var payload = entry.PayloadJson;

            total++;

            if (tick < minTick) minTick = tick;
            if (tick > maxTick) maxTick = tick;

            byKind.TryGetValue(kind, out var kc);
            byKind[kind] = kc + 1;

            byTick.TryGetValue(tick, out var tc);
            byTick[tick] = tc + 1;

            if (!string.IsNullOrWhiteSpace(payload))
                TryAccumulateIntFields(dist, kind, payload);
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

        var roDist = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyDictionary<int, long>>>(StringComparer.Ordinal);
        foreach (var (k, fields) in dist)
        {
            var roFields = new Dictionary<string, IReadOnlyDictionary<int, long>>(StringComparer.Ordinal);
            foreach (var (f, map) in fields)
                roFields[f] = map;
            roDist[k] = roFields;
        }

        return new EventLogStats(
            TotalEvents: total,
            MinTick: minTick,
            MaxTick: maxTick,
            ByKind: byKind,
            ByTick: byTick,
            TopTicks: topTicks,
            TopKinds: topKinds,
            IntFieldDistributions: roDist);
    }

    private static void TryAccumulateIntFields(
        Dictionary<string, Dictionary<string, Dictionary<int, long>>> dist,
        string kind,
        string payload)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return;

            foreach (var p in doc.RootElement.EnumerateObject())
            {
                if (p.Value.ValueKind != JsonValueKind.Number) continue;
                if (!p.Value.TryGetInt32(out var iv)) continue;

                if (!dist.TryGetValue(kind, out var fields))
                {
                    fields = new Dictionary<string, Dictionary<int, long>>(StringComparer.Ordinal);
                    dist[kind] = fields;
                }

                if (!fields.TryGetValue(p.Name, out var map))
                {
                    map = new Dictionary<int, long>();
                    fields[p.Name] = map;
                }

                map.TryGetValue(iv, out var c);
                map[iv] = c + 1;
            }
        }
        catch (JsonException)
        {
        }
    }
}
