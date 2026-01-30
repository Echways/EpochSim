namespace EpochSim.Serialization.Snapshots;

public static class SnapshotLocator
{
    public static string? FindBestSnapshot(string directory, long tickInclusive)
    {
        if (!Directory.Exists(directory)) return null;

        var bestTick = long.MinValue;
        string? bestPath = null;

        foreach (var file in Directory.EnumerateFiles(directory, "snapshot-*.json"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (!name.StartsWith("snapshot-", StringComparison.Ordinal)) continue;

            var s = name.Substring("snapshot-".Length);
            if (!long.TryParse(s, out var t)) continue;

            if (t <= tickInclusive && t > bestTick)
            {
                bestTick = t;
                bestPath = file;
            }
        }

        return bestPath;
    }
}