namespace EpochSim.Execution.RunArtifacts;

public static class RunMetaReader
{
    public static Dictionary<string, string> Read(string metaPath)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!File.Exists(metaPath))
            return values;

        foreach (var line in File.ReadLines(metaPath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var separatorIndex = line.IndexOf('=', StringComparison.Ordinal);
            if (separatorIndex <= 0) continue;

            var key = line.Substring(0, separatorIndex).Trim();
            var valueText = line.Substring(separatorIndex + 1).Trim();
            if (key.Length == 0) continue;

            values[key] = valueText;
        }

        return values;
    }

    public static bool TryGetLong(Dictionary<string, string> meta, string key, out long value)
    {
        if (meta.TryGetValue(key, out var raw) && long.TryParse(raw, out value))
            return true;

        value = 0;
        return false;
    }

    public static bool TryGetUlong(Dictionary<string, string> meta, string key, out ulong value)
    {
        if (meta.TryGetValue(key, out var raw) && ulong.TryParse(raw, out value))
            return true;

        value = 0;
        return false;
    }
}
