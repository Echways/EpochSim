using EpochSim.Cli.App;
using EpochSim.Cli.Parsing;
using EpochSim.Execution;
using EpochSim.Execution.Snapshots;
using EpochSim.Serialization.State;
using EpochSim.Samples.Population;

namespace EpochSim.Cli.Commands;

public sealed class FastReplayCommand : ICliCommand
{
    public int Execute(CommandContext ctx, string[] args)
    {
        var runArg = args.Length > 0 ? args[0] : "";
        var endTick = args.Length > 1 && CliParsing.TryParseLong(args[1], out var et) ? et : 500;
        var seed = args.Length > 3 && CliParsing.TryParseUlong(args[3], out var sd) ? sd : 12345UL;

        var codec = (PopulationEventCodec)ctx.Codec;
        var stateSerializer = ctx.StateSerializer;

        var runId = CliParsing.ResolveRunIdWithEvents(ctx.Root, runArg);
        var paths = CliParsing.Paths(ctx.Root, runId);

        var engine = new SimulationEngine<WorldState>();
        engine.AddSystem(new PopulationSystem());

        var world = SnapshotRunner.LoadBestAndReplayTo(
            engine: engine,
            snapshotsDir: paths.SnapshotsDir,
            eventsPath: paths.EventsPath,
            serializer: stateSerializer,
            codec: codec,
            seed: seed,
            endTick: endTick,
            newState: () => new WorldState());

        Console.WriteLine($"RunDir={paths.RunDir}");
        Console.WriteLine($"RunId={paths.RunId}");
        Console.WriteLine($"Population={world.Population}, Fires={world.Fires}");
        Console.WriteLine($"EndTick={endTick}");

        return 0;
    }
}