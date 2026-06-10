namespace EpochSim.Serialization.EventLog;

public static class EventLogReader
{
    public static IEnumerable<EventLogEntryV2> ReadStream(string path)
    {
        using var stream = OpenRead(path);
        using var reader = new StreamReader(stream);

        var lineNumber = 0;
        while (true)
        {
            var line = reader.ReadLine();
            lineNumber++;
            if (line is null) yield break;
            if (string.IsNullOrWhiteSpace(line)) continue;

            EventLogEntryV2 entry;
            try
            {
                entry = EventLogLine.ReadEntry(line);
            }
            catch (Exception ex) when (ex is FormatException or System.Text.Json.JsonException)
            {
                var fragment = line.Length > 80 ? line[..80] + "…" : line;
                throw new FormatException(
                    $"Line {lineNumber}: {ex.Message} — content: {fragment}", ex);
            }
            yield return entry;
        }
    }

    public static IReadOnlyList<EventLogEntryV2> ReadAll(string path)
        => ReadStream(path).ToArray();

    public static IReadOnlyList<EventLogEntryV2> ReadAfterTick(string path, long tickExclusive)
        => ReadStream(path).Where(e => e.Tick > tickExclusive).ToArray();

    private static Stream OpenRead(string path)
    {
        var resolvedPath = ResolvePath(path);
        var fileStream = new FileStream(resolvedPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (resolvedPath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
            return new System.IO.Compression.GZipStream(fileStream, System.IO.Compression.CompressionMode.Decompress);
        return fileStream;
    }

    private static string ResolvePath(string path)
    {
        if (File.Exists(path))
            return path;

        if (!path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
        {
            var gzPath = path + ".gz";
            if (File.Exists(gzPath))
                return gzPath;
        }

        return path;
    }
}
