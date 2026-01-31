namespace EpochSim.Execution.RunArtifacts;

public static class MinReproMetaReader
{
    public static Dictionary<string, string> Read(string metaPath)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!File.Exists(metaPath))
            return dict;

        foreach (var line in File.ReadLines(metaPath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var idx = line.IndexOf('=', StringComparison.Ordinal);
            if (idx <= 0) continue;

            var key = line.Substring(0, idx).Trim();
            var val = line.Substring(idx + 1).Trim();
            if (key.Length == 0) continue;

            dict[key] = val;
        }

        return dict;
    }

    public static bool TryGetLong(Dictionary<string, string> meta, string key, out long value)
    {
        if (meta.TryGetValue(key, out var s) && long.TryParse(s, out value))
            return true;

        value = 0;
        return false;
    }

    public static bool TryGetUlong(Dictionary<string, string> meta, string key, out ulong value)
    {
        if (meta.TryGetValue(key, out var s) && ulong.TryParse(s, out value))
            return true;

        value = 0;
        return false;
    }
}
