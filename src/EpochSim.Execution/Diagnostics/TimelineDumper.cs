using EpochSim.Serialization.EventLog;

namespace EpochSim.Execution.Diagnostics;

public static class TimelineDumper
{
    public static void Dump(
        string eventsPath,
        long fromTickInclusive,
        long toTickInclusive,
        int maxEventsPerTick,
        int maxPayloadChars,
        IEventPayloadFormatter formatter,
        ISet<string>? allowedKinds)
    {
        if (toTickInclusive < fromTickInclusive)
            (fromTickInclusive, toTickInclusive) = (toTickInclusive, fromTickInclusive);

        var matchedTotal = 0L;

        var currentTick = (long?)null;
        var emittedForTick = 0;
        var totalForTick = 0;
        var buffer = new List<(string Kind, string Payload)>();

        void Flush()
        {
            if (currentTick is null) return;

            Console.WriteLine($"Tick {currentTick.Value} (events={totalForTick})");

            var shown = 0;
            foreach (var (k, p) in buffer)
            {
                if (shown >= maxEventsPerTick) break;

                var payload = p ?? "";
                if (formatter.TryFormat(k, payload, out var pretty))
                    payload = pretty;

                payload = NormalizePayload(payload);
                if (payload.Length > maxPayloadChars)
                    payload = payload.Substring(0, maxPayloadChars) + "...";

                if (payload.Length == 0)
                    Console.WriteLine($"  - {k}");
                else
                    Console.WriteLine($"  - {k} {payload}");

                shown++;
            }

            if (totalForTick > maxEventsPerTick)
                Console.WriteLine($"  ... +{totalForTick - maxEventsPerTick} more");

            Console.WriteLine();
        }

        foreach (var line in File.ReadLines(eventsPath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var tick = EventLogLine.ReadTick(line);
            if (tick < fromTickInclusive) continue;
            if (tick > toTickInclusive) break;

            var kind = EventLogLine.ReadKind(line);
            if (allowedKinds is not null && allowedKinds.Count > 0 && !allowedKinds.Contains(kind))
                continue;

            var payload = EventLogLine.ReadPayload(line);

            matchedTotal++;

            if (currentTick is null)
            {
                currentTick = tick;
                totalForTick = 0;
                emittedForTick = 0;
                buffer.Clear();
            }

            if (tick != currentTick.Value)
            {
                Flush();
                currentTick = tick;
                totalForTick = 0;
                emittedForTick = 0;
                buffer.Clear();
            }

            totalForTick++;
            if (emittedForTick < maxEventsPerTick)
            {
                buffer.Add((kind, payload));
                emittedForTick++;
            }
        }

        Flush();

        if (matchedTotal == 0)
            Console.WriteLine($"No events matched range {fromTickInclusive}..{toTickInclusive}" + (allowedKinds is null || allowedKinds.Count == 0 ? "" : $" and filter [{string.Join(",", allowedKinds)}]"));
    }

    private static string NormalizePayload(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.Replace('\n', ' ').Replace('\r', ' ').Replace('\t', ' ');
        while (s.Contains("  ", StringComparison.Ordinal))
            s = s.Replace("  ", " ", StringComparison.Ordinal);
        return s.Trim();
    }

}
