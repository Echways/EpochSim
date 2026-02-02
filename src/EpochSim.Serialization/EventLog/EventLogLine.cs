namespace EpochSim.Serialization.EventLog;

public static class EventLogLine
{
    public static long ReadTick(string line) => ReadLongField(line, "\"t\":");
    public static string ReadKind(string line) => ReadStringField(line, "\"kind\":");
    public static string ReadPayload(string line) => ReadStringField(line, "\"payload\":");

    public static long ReadLongField(string line, string field)
    {
        var i = line.IndexOf(field, StringComparison.Ordinal);
        if (i < 0) throw new FormatException($"Missing field {field}");

        i += field.Length;
        while (i < line.Length && char.IsWhiteSpace(line[i])) i++;
        var end = i;
        while (end < line.Length && (char.IsDigit(line[end]) || line[end] == '-')) end++;

        var span = line.AsSpan(i, end - i);
        if (!long.TryParse(span, out var v)) throw new FormatException($"Invalid number for {field}");

        return v;
    }

    public static string ReadStringField(string line, string field)
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
