namespace EpochSim.Serialization.EventLog;

public static class EventLogReader
{
    public static IReadOnlyList<EventLogEntry> ReadAll(string path)
    {
        var list = new List<EventLogEntry>();

        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var tick = ReadLongField(line, "\"t\":");
            var kind = ReadStringField(line, "\"kind\":");
            var payload = ReadStringField(line, "\"payload\":");

            list.Add(new EventLogEntry(tick, kind, payload));
        }

        return list;
    }

    public static IReadOnlyList<EventLogEntry> ReadAfterTick(string path, long tickExclusive)
    {
        var all = ReadAll(path);
        return all.Where(e => e.Tick > tickExclusive).ToArray();
    }

    private static long ReadLongField(string line, string field)
    {
        var i = line.IndexOf(field, StringComparison.Ordinal);
        if (i < 0) throw new FormatException($"Missing field {field}");

        i += field.Length;
        var end = i;
        while (end < line.Length && (char.IsDigit(line[end]) || line[end] == '-')) end++;

        var span = line.AsSpan(i, end - i);
        if (!long.TryParse(span, out var v)) throw new FormatException($"Invalid number for {field}");

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
