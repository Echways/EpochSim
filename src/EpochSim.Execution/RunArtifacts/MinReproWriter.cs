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

        var snapshotOutPath = Path.Combine(dir, "snapshot.json");
        if (bestSnapPath is null)
        {
            var stateJson = serializer.Serialize(newState());
            SnapshotWriter.Write(snapshotOutPath, 0, stateJson);
            snapTick = 0;
        }
        else
        {
            File.Copy(bestSnapPath, snapshotOutPath, overwrite: true);
            var snap = SnapshotReader.Read(snapshotOutPath);
            snapTick = snap.Tick;
        }

        var eventsOutPath = Path.Combine(dir, RunPaths.EventsFileName);
        WriteEventsTail(paths.ResolveEventsPath(), eventsOutPath, snapTick, failureTick);

        var metaOutPath = Path.Combine(dir, RunPaths.MetaFileName);
        File.WriteAllText(metaOutPath,
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
