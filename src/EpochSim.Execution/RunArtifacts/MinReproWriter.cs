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
        WriteEventsTail(paths.EventsPath, eventsOut, snapTick, failureTick);

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
        using var fs = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        using var w = new StreamWriter(fs, new System.Text.UTF8Encoding(false));

        foreach (var line in File.ReadLines(eventsPath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var t = ReadTick(line);
            if (t > tickExclusive && t <= tickInclusive)
                w.WriteLine(line);
        }
    }

    private static long ReadTick(string line)
    {
        var field = "\"t\":";
        var i = line.IndexOf(field, StringComparison.Ordinal);
        if (i < 0) throw new FormatException("Missing tick field");

        i += field.Length;
        while (i < line.Length && char.IsWhiteSpace(line[i])) i++;

        var end = i;
        while (end < line.Length && (char.IsDigit(line[end]) || line[end] == '-')) end++;

        var span = line.AsSpan(i, end - i);
        if (!long.TryParse(span, out var v)) throw new FormatException("Invalid tick value");

        return v;
    }
}
