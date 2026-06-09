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
    public string Help =>
        """
        bisect <artifactsRoot> [runId] [--end-tick N] [--seed N]

        Binary-search the event log to find the first tick where an invariant is violated.
        Returns exit code 2 with FirstViolationTick when a violation is found, 0 otherwise.

          --end-tick N   Last tick to search (default: from manifest/meta or 500)
          --seed N       RNG seed override (default: from manifest/meta or 12345)
        """;

    protected override int Execute<TState>(IDomainAdapter<TState> adapter, CommandContext ctx, string[] args)
    {
        var positional = new List<string>();
        long? endTickOpt = null;
        ulong? seedOpt = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (!CliParsing.IsOption(arg))
            {
                positional.Add(arg);
                continue;
            }

            switch (arg.ToLowerInvariant())
            {
                case "--end-tick":
                    if (i + 1 < args.Length && CliParsing.TryParseLong(args[i + 1], out var et)) { endTickOpt = et; i++; }
                    break;
                case "--seed":
                    if (i + 1 < args.Length && CliParsing.TryParseUlong(args[i + 1], out var sd)) { seedOpt = sd; i++; }
                    break;
            }
        }

        var runArg = positional.Count > 0 ? positional[0] : "";

        var codec = adapter.Codec;
        var stateSerializer = adapter.Serializer;

        var runId = CliParsing.ResolveRunIdWithEvents(ctx.Root, runArg);
        var paths = new RunPaths(ctx.Root, runId);

        if (!Directory.Exists(paths.RunDir))
            throw new DirectoryNotFoundException($"RunDir not found: {paths.RunDir}");

        var meta = RunMetaReader.Read(paths.MetaPath);
        var manifest = RunManifestReader.TryRead(paths.ManifestPath);

        var endTick = endTickOpt
            ?? (positional.Count > 1 && CliParsing.TryParseLong(positional[1], out var endTickPos) ? endTickPos : (long?)null)
            ?? manifest?.EndTick ?? (RunMetaReader.TryGetLong(meta, "endTick", out var endTickMeta) ? endTickMeta : 500);

        var seed = seedOpt
            ?? (positional.Count > 3 && CliParsing.TryParseUlong(positional[3], out var seedPos) ? seedPos : (ulong?)null)
            ?? manifest?.Seed ?? (RunMetaReader.TryGetUlong(meta, "seed", out var seedMeta) ? seedMeta : 12345UL);

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
