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
        var manifest = RunManifestReader.TryRead(paths.ManifestPath);

        var endTick = args.Length > 1 && CliParsing.TryParseLong(args[1], out var endTickArg) ? endTickArg
            : (manifest?.EndTick ?? (RunMetaReader.TryGetLong(meta, "endTick", out var endTickMeta) ? endTickMeta : 500));

        var seed = args.Length > 3 && CliParsing.TryParseUlong(args[3], out var seedArg) ? seedArg
            : (manifest?.Seed ?? (RunMetaReader.TryGetUlong(meta, "seed", out var seedMeta) ? seedMeta : 12345UL));

        var fingerprintEvery = manifest?.FingerprintEvery
            ?? (RunMetaReader.TryGetLong(meta, "fingerprintEvery", out var fingerprintEveryMeta) ? fingerprintEveryMeta : 1);
        if (fingerprintEvery <= 0) fingerprintEvery = 1;

        var expected = JsonlStateFingerprintWriter.ReadAll(paths.StateFpPath);

        var engine = new SimulationEngine<WorldState>();
        engine.AddSystem(new PopulationSystem());

        var sink = new InMemoryStateFingerprintSink();
        var tmpState = new WorldState();
        engine.AddMiddleware(new StateFingerprintMiddleware<WorldState>(tmpState, stateSerializer, sink, fingerprintEvery));

        var replayed = SnapshotRunner.LoadBestAndReplayTo(
            engine: engine,
            snapshotsDir: paths.SnapshotsDir,
            eventsPath: paths.ResolveEventsPath(),
            serializer: stateSerializer,
            codec: codec,
            seed: seed,
            endTick: endTick,
            newState: () => tmpState);

        var mismatch = FindFirstMismatch(expected, sink.Records, endTick, fingerprintEvery);

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
        long endTick,
        long fingerprintEvery)
    {
        var actualMap = new Dictionary<long, string>();
        foreach (var r in actual)
            actualMap[r.Tick] = r.Hash ?? "<null>";

        for (var t = 0L; t <= endTick; t++)
        {
            if (fingerprintEvery > 1 && t % fingerprintEvery != 0)
                continue;

            var hasE = expected.TryGetValue(t, out var expectedRaw);
            var hasA = actualMap.TryGetValue(t, out var actualRaw);

            var expectedValue = expectedRaw ?? "<null>";
            var actualValue = actualRaw ?? "<null>";

            if (!hasE && hasA)
                return (t, "<missing>", actualValue);

            if (hasE && !hasA)
                return (t, expectedValue, "<missing>");

            if (hasE && hasA && !string.Equals(expectedValue, actualValue, StringComparison.Ordinal))
                return (t, expectedValue, actualValue);
        }

        return null;
    }
}
