using System;
using System.Collections.Generic;
using EpochSim.Cli.App;
using EpochSim.Cli.Domain;
using EpochSim.Cli.Parsing;
using EpochSim.Execution;
using EpochSim.Execution.Middleware;
using EpochSim.Execution.RunArtifacts;
using EpochSim.Execution.Snapshots;
using EpochSim.Kernel.Determinism;
using EpochSim.Kernel.Validation;

namespace EpochSim.Cli.Commands;

public sealed class BisectCommand : DomainCommandBase
{
    protected override int Execute<TState>(IDomainAdapter<TState> adapter, CommandContext ctx, string[] args)
    {
        var runArg = args.Length > 0 ? args[0] : "";
        var endTickArg = args.Length > 1 ? args[1] : null;
        var seedArg = args.Length > 3 ? args[3] : null;

        var codec = adapter.Codec;
        var stateSerializer = adapter.Serializer;

        var runId = CliParsing.ResolveRunIdWithEvents(ctx.Root, runArg);
        var paths = new RunPaths(ctx.Root, runId);

        if (!Directory.Exists(paths.RunDir))
            throw new DirectoryNotFoundException($"RunDir not found: {paths.RunDir}");

        var meta = RunMetaReader.Read(paths.MetaPath);
        var manifest = RunManifestReader.TryRead(paths.ManifestPath);

        var endTick = endTickArg is not null && CliParsing.TryParseLong(endTickArg, out var endTickParsed) ? endTickParsed
            : (manifest?.EndTick ?? (RunMetaReader.TryGetLong(meta, "endTick", out var endTickMeta) ? endTickMeta : 500));

        var seed = seedArg is not null && CliParsing.TryParseUlong(seedArg, out var seedParsed) ? seedParsed
            : (manifest?.Seed ?? (RunMetaReader.TryGetUlong(meta, "seed", out var seedMeta) ? seedMeta : 12345UL));

        var eventsPath = paths.ResolveEventsPath();
        if (!File.Exists(eventsPath))
            throw new FileNotFoundException($"events log not found: {eventsPath}");

        if (!Directory.Exists(paths.SnapshotsDir))
            Directory.CreateDirectory(paths.SnapshotsDir);

        var options = new RunOptions
        {
            RngVersion = ResolveRngVersion(manifest?.RngVersion)
        };

        bool FailsUpTo(long probeTick, out InvariantViolationException? ex)
        {
            var engine = new SimulationEngine<TState>();
            adapter.ConfigureEngine(engine);

            var memLog = new InMemoryEventLogMiddleware(codec);
            engine.AddMiddleware(memLog);

            var invariants = new List<IInvariant<TState>>();
            invariants.AddRange(adapter.CreateInvariants());
            invariants.Add(new MaxEventsPerTickInvariant<TState>(() => memLog.EventsThisTick, maxEvents: 1000));

            var world = adapter.CreateInitialState();
            engine.AddMiddleware(new InvariantMiddleware<TState>(world, invariants, checkEveryTicks: 1));

            try
            {
                SnapshotRunner.LoadBestAndReplayTo(
                    engine: engine,
                    snapshotsDir: paths.SnapshotsDir,
                    eventsPath: eventsPath,
                    serializer: stateSerializer,
                    codec: codec,
                    seed: seed,
                    endTick: probeTick,
                    newState: () => world,
                    options: options,
                    cancellationToken: ctx.Cancellation);

                ex = null;
                return false;
            }
            catch (InvariantViolationException e)
            {
                ex = e;
                return true;
            }
        }

        Console.WriteLine($"RunDir={paths.RunDir}");
        Console.WriteLine($"RunId={paths.RunId}");
        Console.WriteLine($"EndTick={endTick}");
        Console.WriteLine($"Seed={seed}");

        if (!FailsUpTo(endTick, out var exAtEnd))
        {
            Console.WriteLine("NoInvariantViolationUpToEndTick");
            return 0;
        }

        var hi = exAtEnd!.Time.Tick;
        var lo = 0L;

        while (lo < hi)
        {
            var mid = lo + ((hi - lo) / 2);

            if (FailsUpTo(mid, out var exMid))
                hi = exMid!.Time.Tick;
            else
                lo = mid + 1;
        }

        FailsUpTo(lo, out var exFinal);

        Console.WriteLine($"FirstViolationTick={lo}");

        if (exFinal is not null)
        {
            Console.WriteLine($"Invariant={exFinal.InvariantName}");
            Console.WriteLine($"Message={exFinal.Detail}");

            var minDir = MinReproWriter.Create(
                paths: paths,
                failureTick: lo,
                seed: seed,
                endTick: endTick,
                invariantName: exFinal.InvariantName,
                invariantMessage: exFinal.Detail,
                serializer: stateSerializer,
                newState: adapter.CreateInitialState);

            Console.WriteLine($"MinReproDir={minDir}");
            Console.WriteLine("MinReproFiles=snapshot.json,events.jsonl,meta.txt");
        }

        return 2;
    }

    private static RngVersion ResolveRngVersion(string? raw)
    {
        if (string.Equals(raw, "V1", StringComparison.OrdinalIgnoreCase))
            return RngVersion.V1;

        if (string.Equals(raw, "V2", StringComparison.OrdinalIgnoreCase))
            return RngVersion.V2;

        return RngVersion.V2;
    }
}
