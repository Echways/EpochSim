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
        var index = text.IndexOf(field, StringComparison.Ordinal);
        if (index < 0) throw new FormatException($"Missing field {field}");

        index += field.Length;
        while (index < text.Length && char.IsWhiteSpace(text[index])) index++;

        var endIndex = index;
        while (endIndex < text.Length && (char.IsDigit(text[endIndex]) || text[endIndex] == '-')) endIndex++;

        var span = text.AsSpan(index, endIndex - index);
        if (!long.TryParse(span, out var value)) throw new FormatException($"Invalid number for {field}");

        return value;
    }

    private static string ReadJsonValue(string text, string field)
    {
        var index = text.IndexOf(field, StringComparison.Ordinal);
        if (index < 0) throw new FormatException($"Missing field {field}");

        index += field.Length;
        while (index < text.Length && char.IsWhiteSpace(text[index])) index++;

        if (index >= text.Length) throw new FormatException($"Invalid JSON for {field}");

        var startIndex = index;

        if (text[index] == '"')
        {
            index++;
            while (index < text.Length)
            {
                var ch = text[index++];
                if (ch == '\\')
                {
                    if (index < text.Length) index++;
                    continue;
                }
                if (ch == '"') break;
            }
            return text.Substring(startIndex, index - startIndex);
        }

        if (text[index] == '{' || text[index] == '[')
        {
            var open = text[index];
            var close = open == '{' ? '}' : ']';

            var depth = 0;
            var inString = false;

            while (index < text.Length)
            {
                var ch = text[index++];

                if (inString)
                {
                    if (ch == '\\')
                    {
                        if (index < text.Length) index++;
                        continue;
                    }
                    if (ch == '"') inString = false;
                    continue;
                }

                if (ch == '"')
                {
                    inString = true;
                    continue;
                }

                if (ch == open) depth++;
                if (ch == close) depth--;

                if (depth == 0) break;
            }

            return text.Substring(startIndex, index - startIndex);
        }

        var endIndex = index;
        while (endIndex < text.Length && text[endIndex] != ',' && text[endIndex] != '}' && !char.IsWhiteSpace(text[endIndex])) endIndex++;
        return text.Substring(startIndex, endIndex - startIndex);
    }
}
