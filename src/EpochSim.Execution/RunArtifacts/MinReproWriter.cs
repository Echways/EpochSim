using System.Text.Json;
using EpochSim.Serialization.Snapshots;
using EpochSim.Serialization.State;

namespace EpochSim.Execution.RunArtifacts;

public static class MinReproWriter
{
    public static string Create<TState>(
        RunPaths paths,
        long failureTick,
        ulong seed,
        long endTick,
        string invariantName,
        string invariantMessage,
        IStateSerializer<TState> serializer,
        Func<TState> newState)
    {
        var dir = Path.Combine(paths.RunDir, "minrepro", $"tick-{failureTick}");
        Directory.CreateDirectory(dir);

        var bestSnapPath = SnapshotLocator.FindBestSnapshot(paths.SnapshotsDir, Math.Max(0, failureTick - 1));
        long snapTick;

        var snapOut = Path.Combine(dir, "snapshot.json");
        if (bestSnapPath is null)
        {
            var stateJson = serializer.Serialize(newState());
            SnapshotWriter.Write(snapOut, 0, stateJson);
            snapTick = 0;
        }
        else
        {
            File.Copy(bestSnapPath, snapOut, overwrite: true);
            var snap = SnapshotReader.Read(snapOut);
            snapTick = snap.Tick;
        }

        var eventsOut = Path.Combine(dir, "events.jsonl");
        WriteEventsTail(paths.ResolveEventsPath(), eventsOut, snapTick, failureTick);

        var metaOut = Path.Combine(dir, "meta.txt");
        File.WriteAllText(metaOut,
            $"runId={paths.RunId}\n" +
            $"failureTick={failureTick}\n" +
            $"snapshotTick={snapTick}\n" +
            $"seed={seed}\n" +
            $"endTick={endTick}\n" +
            $"invariant={invariantName}\n" +
            $"message={invariantMessage}\n");

        return dir;
    }

    private static void WriteEventsTail(string eventsPath, string outPath, long tickExclusive, long tickInclusive)
    {
        using var fileStream = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(fileStream, new System.Text.UTF8Encoding(false));

        using var input = OpenRead(eventsPath);
        using var reader = new StreamReader(input);

        while (reader.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            using var doc = JsonDocument.Parse(line);
            if (!doc.RootElement.TryGetProperty("t", out var tProp)) continue;
            var tick = tProp.GetInt64();
            if (tick > tickExclusive && tick <= tickInclusive)
                writer.WriteLine(line);
        }
    }

    private static Stream OpenRead(string path)
    {
        var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
            return new System.IO.Compression.GZipStream(fileStream, System.IO.Compression.CompressionMode.Decompress);
        return fileStream;
    }
}
