using System;
using System.Collections.Generic;
using EpochSim.Cli.App;
using EpochSim.Cli.Parsing;
using EpochSim.Execution;
using EpochSim.Execution.Middleware;
using EpochSim.Execution.RunArtifacts;
using EpochSim.Execution.StateFingerprint;
using EpochSim.Kernel.Time;
using EpochSim.Kernel.Validation;
using EpochSim.Samples.Population;
using EpochSim.Serialization.EventLog;

namespace EpochSim.Cli.Commands;

public sealed class ValidateRunCommand : ICliCommand
{
    public int Execute(CommandContext ctx, string[] args)
    {
        var runArg = args.Length > 0 ? args[0] : "";
        var endTick = args.Length > 1 && CliParsing.TryParseLong(args[1], out var et) ? et : 500;
        var snapEvery = args.Length > 2 && CliParsing.TryParseLong(args[2], out var se) ? se : 50;
        var seed = args.Length > 3 && CliParsing.TryParseUlong(args[3], out var sd) ? sd : 12345UL;

        var codec = ctx.Codec;
        var stateSerializer = ctx.StateSerializer;

        var runId = string.IsNullOrWhiteSpace(runArg) ? RunId.New() : CliParsing.NormalizeRunId(runArg);
        var paths = new RunPaths(ctx.Root, runId);
        paths.Ensure();
        RunMetaWriter.Write(paths, mode: "validate-run", seed: seed, endTick: endTick, snapEvery: snapEvery);

        var engine = new SimulationEngine<WorldState>();
        engine.AddSystem(new PopulationSystem());
        engine.RegisterCommandHandler(new GrowPopulationHandler());
        engine.RegisterCommandHandler(new ScheduleFireHandler());

        var world = new WorldState();

        var memLog = new InMemoryEventLogMiddleware(codec);
        engine.AddMiddleware(memLog);

        using var eventWriter = new EventLogWriter(paths.EventsPath);
        engine.AddMiddleware(new EventLogMiddleware(eventWriter, codec));

        using var fpWriter = new JsonlStateFingerprintWriter(paths.StateFpPath);
        engine.AddMiddleware(new StateFingerprintMiddleware<WorldState>(world, stateSerializer, fpWriter));

        engine.AddMiddleware(new SnapshotMiddleware<WorldState>(world, snapEvery, paths.SnapshotsDir, stateSerializer));

        var invariants = new List<IInvariant<WorldState>>
        {
            new PopulationNonNegativeInvariant(),
            new MaxEventsPerTickInvariant<WorldState>(() => memLog.EventsThisTick, maxEvents: 1000)
        };

        engine.AddMiddleware(new InvariantMiddleware<WorldState>(world, invariants, checkEveryTicks: 1));

        var dumper = new FailFastDumpMiddleware<WorldState>(
            state: world,
            currentTickProvider: () => memLog.CurrentTick,
            eventLogProvider: () => memLog.Entries,
            serializer: stateSerializer,
            dumpDirectory: paths.DumpsDir);

        try
        {
            engine.RunTicks(world, seed: seed, start: SimTime.Zero, endInclusive: new SimTime(endTick));
            Console.WriteLine($"RunDir={paths.RunDir}");
            Console.WriteLine($"RunId={paths.RunId}");
            Console.WriteLine("ValidationOK");
            Console.WriteLine($"Population={world.Population}, Fires={world.Fires}");
            return 0;
        }
        catch (InvariantViolationException ex)
        {
            dumper.DumpOnViolation(ex);
            Console.WriteLine($"RunDir={paths.RunDir}");
            Console.WriteLine($"RunId={paths.RunId}");
            Console.WriteLine(ex.Message);
            return 2;
        }
    }
}
