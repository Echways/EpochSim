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
    protected override int Execute<TState>(IDomainAdapter<TState> adapter, CommandContext ctx, string[] args)
    {
        var runArg = args.Length > 0 ? args[0] : "";

        var codec = adapter.Codec;
        var stateSerializer = adapter.Serializer;

        var runId = CliParsing.ResolveRunIdWithEvents(ctx.Root, runArg);
        var paths = CliParsing.Paths(ctx.Root, runId);
        var manifest = RunManifestReader.TryRead(paths.ManifestPath);

        var endTick = args.Length > 1 && CliParsing.TryParseLong(args[1], out var endTickArg) ? endTickArg
            : (manifest?.EndTick ?? 500);
        var seed = args.Length > 3 && CliParsing.TryParseUlong(args[3], out var seedArg) ? seedArg
            : (manifest?.Seed ?? 12345UL);

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
