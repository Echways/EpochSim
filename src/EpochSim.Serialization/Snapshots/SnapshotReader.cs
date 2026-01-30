namespace EpochSim.Serialization.Snapshots;

public static class SnapshotReader
{
    public static SnapshotFile Read(string path)
    {
        var text = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(text)) throw new FormatException("Empty snapshot");

        var tick = ReadLongField(text, "\"t\":");
        var stateJson = ReadJsonValue(text, "\"state\":");

        return new SnapshotFile(tick, stateJson);
    }

    private static long ReadLongField(string text, string field)
    {
        var i = text.IndexOf(field, StringComparison.Ordinal);
        if (i < 0) throw new FormatException($"Missing field {field}");

        i += field.Length;
        while (i < text.Length && char.IsWhiteSpace(text[i])) i++;

        var end = i;
        while (end < text.Length && (char.IsDigit(text[end]) || text[end] == '-')) end++;

        var span = text.AsSpan(i, end - i);
        if (!long.TryParse(span, out var v)) throw new FormatException($"Invalid number for {field}");

        return v;
    }

    private static string ReadJsonValue(string text, string field)
    {
        var i = text.IndexOf(field, StringComparison.Ordinal);
        if (i < 0) throw new FormatException($"Missing field {field}");

        i += field.Length;
        while (i < text.Length && char.IsWhiteSpace(text[i])) i++;

        if (i >= text.Length) throw new FormatException($"Invalid JSON for {field}");

        var start = i;

        if (text[i] == '"')
        {
            i++;
            while (i < text.Length)
            {
                var ch = text[i++];
                if (ch == '\\')
                {
                    if (i < text.Length) i++;
                    continue;
                }
                if (ch == '"') break;
            }
            return text.Substring(start, i - start);
        }

        if (text[i] == '{' || text[i] == '[')
        {
            var open = text[i];
            var close = open == '{' ? '}' : ']';

            var depth = 0;
            var inStr = false;

            while (i < text.Length)
            {
                var ch = text[i++];

                if (inStr)
                {
                    if (ch == '\\')
                    {
                        if (i < text.Length) i++;
                        continue;
                    }
                    if (ch == '"') inStr = false;
                    continue;
                }

                if (ch == '"')
                {
                    inStr = true;
                    continue;
                }

                if (ch == open) depth++;
                if (ch == close) depth--;

                if (depth == 0) break;
            }

            return text.Substring(start, i - start);
        }

        var end = i;
        while (end < text.Length && text[end] != ',' && text[end] != '}' && !char.IsWhiteSpace(text[end])) end++;
        return text.Substring(start, end - start);
    }
}
