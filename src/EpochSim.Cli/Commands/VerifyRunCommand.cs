using System;
using System.Collections.Generic;
using EpochSim.Cli.App;
using EpochSim.Cli.Parsing;
using EpochSim.Execution;
using EpochSim.Execution.Middleware;
using EpochSim.Execution.RunArtifacts;
using EpochSim.Execution.Snapshots;
using EpochSim.Execution.StateFingerprint;
using EpochSim.Samples.Population;

namespace EpochSim.Cli.Commands;

public sealed class VerifyRunCommand : ICliCommand
{
    public int Execute(CommandContext ctx, string[] args)
    {
        var runArg = args.Length > 0 ? args[0] : "";

        var codec = ctx.Codec;
        var stateSerializer = ctx.StateSerializer;

        var runId = CliParsing.ResolveRunIdWithEvents(ctx.Root, runArg);
        var paths = CliParsing.Paths(ctx.Root, runId);

        if (!File.Exists(paths.StateFpPath))
            throw new FileNotFoundException($"statefp.jsonl not found: {paths.StateFpPath}");

        var meta = RunMetaReader.Read(paths.MetaPath);

        var endTick = args.Length > 1 && CliParsing.TryParseLong(args[1], out var et) ? et
            : (RunMetaReader.TryGetLong(meta, "endTick", out var em) ? em : 500);

        var seed = args.Length > 3 && CliParsing.TryParseUlong(args[3], out var sd) ? sd
            : (RunMetaReader.TryGetUlong(meta, "seed", out var sm) ? sm : 12345UL);

        var expected = JsonlStateFingerprintWriter.ReadAll(paths.StateFpPath);

        var engine = new SimulationEngine<WorldState>();
        engine.AddSystem(new PopulationSystem());

        var sink = new InMemoryStateFingerprintSink();
        var tmpState = new WorldState();
        engine.AddMiddleware(new StateFingerprintMiddleware<WorldState>(tmpState, stateSerializer, sink));

        var replayed = SnapshotRunner.LoadBestAndReplayTo(
            engine: engine,
            snapshotsDir: paths.SnapshotsDir,
            eventsPath: paths.EventsPath,
            serializer: stateSerializer,
            codec: codec,
            seed: seed,
            endTick: endTick,
            newState: () => tmpState);

        var mismatch = FindFirstMismatch(expected, sink.Records, endTick);

        Console.WriteLine($"RunDir={paths.RunDir}");
        Console.WriteLine($"RunId={paths.RunId}");
        Console.WriteLine($"EndTick={endTick}");
        Console.WriteLine($"Seed={seed}");

        if (mismatch is null)
        {
            Console.WriteLine("VerifyOK");
            Console.WriteLine($"Population={replayed.Population}, Fires={replayed.Fires}");
            return 0;
        }

        Console.WriteLine($"VerifyMismatchTick={mismatch.Value.Tick}");
        Console.WriteLine($"Expected={mismatch.Value.Expected}");
        Console.WriteLine($"Actual={mismatch.Value.Actual}");
        return 3;
    }

    private static (long Tick, string Expected, string Actual)? FindFirstMismatch(
        Dictionary<long, string> expected,
        IReadOnlyList<(long Tick, string Hash)> actual,
        long endTick)
    {
        var actualMap = new Dictionary<long, string>();
        foreach (var r in actual)
            actualMap[r.Tick] = r.Hash ?? "<null>";

        for (var t = 0L; t <= endTick; t++)
        {
            var hasE = expected.TryGetValue(t, out var eRaw);
            var hasA = actualMap.TryGetValue(t, out var aRaw);

            var e = eRaw ?? "<null>";
            var a = aRaw ?? "<null>";

            if (!hasE && hasA)
                return (t, "<missing>", a);

            if (hasE && !hasA)
                return (t, e, "<missing>");

            if (hasE && hasA && !string.Equals(e, a, StringComparison.Ordinal))
                return (t, e, a);
        }

        return null;
    }
}
