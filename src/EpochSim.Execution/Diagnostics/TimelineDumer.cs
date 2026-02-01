namespace EpochSim.Execution.Diagnostics;

public static class TimelineDumper
{
    public static void Dump(
        string eventsPath,
        long fromTickInclusive,
        long toTickInclusive,
        int maxEventsPerTick,
        int maxPayloadChars,
        IEventPayloadFormatter formatter)
    {
        if (toTickInclusive < fromTickInclusive)
            (fromTickInclusive, toTickInclusive) = (toTickInclusive, fromTickInclusive);

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
                    payload = payload.Substring(0, maxPayloadChars) + "…";

                if (payload.Length == 0)
                    Console.WriteLine($"  - {k}");
                else
                    Console.WriteLine($"  - {k} {payload}");

                shown++;
            }

            if (totalForTick > maxEventsPerTick)
                Console.WriteLine($"  … +{totalForTick - maxEventsPerTick} more");

            Console.WriteLine();
        }

        foreach (var line in File.ReadLines(eventsPath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var tick = ReadLongField(line, "\"t\":");
            if (tick < fromTickInclusive) continue;
            if (tick > toTickInclusive) break;

            var kind = ReadStringField(line, "\"kind\":");
            var payload = ReadStringField(line, "\"payload\":");

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
    }

    private static string NormalizePayload(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.Replace('\n', ' ').Replace('\r', ' ').Replace('\t', ' ');
        while (s.Contains("  ", StringComparison.Ordinal))
            s = s.Replace("  ", " ", StringComparison.Ordinal);
        return s.Trim();
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
