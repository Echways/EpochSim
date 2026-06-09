using EpochSim.Cli.App;
using EpochSim.Cli.Domain;
using EpochSim.Cli.Parsing;
using EpochSim.Execution;
using EpochSim.Execution.RunArtifacts;
using EpochSim.Execution.Snapshots;
using EpochSim.Kernel.Determinism;

namespace EpochSim.Cli.Commands;

public sealed class FastReplayCommand : DomainCommandBase
{
    public string Help =>
        """
        fast-replay <artifactsRoot> [runId] [--end-tick N] [--seed N]

        Replay the simulation from the best available snapshot + event log.
        Reads endTick and seed from the run manifest when not specified.

          --end-tick N   Last tick to replay to (default: from manifest or 500)
          --seed N       RNG seed override (default: from manifest or 12345)
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
        var paths = CliParsing.Paths(ctx.Root, runId);
        var manifest = RunManifestReader.TryRead(paths.ManifestPath);

        var endTick = endTickOpt
            ?? (positional.Count > 1 && CliParsing.TryParseLong(positional[1], out var endTickPos) ? endTickPos : (long?)null)
            ?? manifest?.EndTick ?? 500;
        var seed = seedOpt
            ?? (positional.Count > 3 && CliParsing.TryParseUlong(positional[3], out var seedPos) ? seedPos : (ulong?)null)
            ?? manifest?.Seed ?? 12345UL;

        var engine = new SimulationEngine<TState>();
        adapter.ConfigureEngine(engine);

        var options = new RunOptions
        {
            RngVersion = ResolveRngVersion(manifest?.RngVersion)
        };

        var world = SnapshotRunner.LoadBestAndReplayTo(
            engine: engine,
            snapshotsDir: paths.SnapshotsDir,
            eventsPath: paths.ResolveEventsPath(),
            serializer: stateSerializer,
            codec: codec,
            seed: seed,
            endTick: endTick,
            newState: adapter.CreateInitialState,
            options: options,
            cancellationToken: ctx.Cancellation);

        Console.WriteLine($"RunDir={paths.RunDir}");
        Console.WriteLine($"RunId={paths.RunId}");
        foreach (var line in adapter.DescribeState(world))
            Console.WriteLine(line);
        Console.WriteLine($"EndTick={endTick}");

        return 0;
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
