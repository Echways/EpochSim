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

            var tickText = name.Substring("snapshot-".Length);
            if (!long.TryParse(tickText, out var tick)) continue;

            if (tick <= tickInclusive && tick > bestTick)
            {
                bestTick = tick;
                bestPath = file;
            }
        }

        return bestPath;
    }
}
