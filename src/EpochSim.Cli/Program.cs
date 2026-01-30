using EpochSim.Execution;
using EpochSim.Execution.Middleware;
using EpochSim.Execution.Snapshots;
using EpochSim.Execution.Validation;
using EpochSim.Kernel.Time;
using EpochSim.Kernel.Validation;
using EpochSim.Samples.Population;
using EpochSim.Serialization.EventLog;
using EpochSim.Serialization.State;

var mode = args.Length > 0 ? args[0] : "run";
var eventsPath = args.Length > 1 ? args[1] : "events.jsonl";
var snapshotsDir = args.Length > 2 ? args[2] : "snapshots";
var endTick = args.Length > 3 && long.TryParse(args[3], out var et) ? et : 60;
var snapEvery = args.Length > 4 && long.TryParse(args[4], out var se) ? se : 20;
var dumpDir = args.Length > 5 ? args[5] : "dumps";

var codec = new PopulationEventCodec();
var stateSerializer = new JsonStateSerializer<WorldState>();

if (mode == "run")
{
    var engine = new SimulationEngine<WorldState>();
    engine.AddSystem(new PopulationSystem());

    engine.RegisterCommandHandler(new GrowPopulationHandler());
    engine.RegisterCommandHandler(new ScheduleFireHandler());

    using var eventWriter = new EventLogWriter(eventsPath);
    engine.AddMiddleware(new EventLogMiddleware(eventWriter, codec));

    var world = new WorldState();
    engine.AddMiddleware(new SnapshotMiddleware<WorldState>(world, snapEvery, snapshotsDir, stateSerializer));

    engine.RunTicks(world, seed: 12345, start: SimTime.Zero, endInclusive: new SimTime(endTick));

    Console.WriteLine($"Population={world.Population}, Fires={world.Fires}");
    Console.WriteLine($"EventLogSaved={eventsPath}");
    Console.WriteLine($"SnapshotsDir={snapshotsDir}");
}
else if (mode == "fast-replay")
{
    var engine = new SimulationEngine<WorldState>();
    engine.AddSystem(new PopulationSystem());

    var world = SnapshotRunner.LoadBestAndReplayTo(
        engine: engine,
        snapshotsDir: snapshotsDir,
        eventsPath: eventsPath,
        serializer: stateSerializer,
        codec: codec,
        seed: 12345,
        endTick: endTick,
        newState: () => new WorldState());

    Console.WriteLine($"Population={world.Population}, Fires={world.Fires}");
    Console.WriteLine($"EndTick={endTick}");
}
else if (mode == "validate-run")
{
    var engine = new SimulationEngine<WorldState>();
    engine.AddSystem(new PopulationSystem());

    engine.RegisterCommandHandler(new GrowPopulationHandler());
    engine.RegisterCommandHandler(new ScheduleFireHandler());

    var world = new WorldState();

    var memLog = new InMemoryEventLogMiddleware(codec);
    engine.AddMiddleware(memLog);

    var invariants = new List<EpochSim.Kernel.Validation.IInvariant<WorldState>>
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
        dumpDirectory: dumpDir);

    try
    {
        engine.RunTicks(world, seed: 12345, start: SimTime.Zero, endInclusive: new SimTime(endTick));
        Console.WriteLine("ValidationOK");
        Console.WriteLine($"Population={world.Population}, Fires={world.Fires}");
    }
    catch (InvariantViolationException ex)
    {
        dumper.DumpOnViolation(ex);
        Console.WriteLine(ex.Message);
        Console.WriteLine($"DumpDir={dumpDir}");
        Environment.ExitCode = 2;
    }
}
else
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  run [events.jsonl] [snapshotsDir] [endTick] [snapEveryTicks]");
    Console.WriteLine("  fast-replay [events.jsonl] [snapshotsDir] [endTick]");
    Console.WriteLine("  validate-run [ignored] [ignored] [endTick] [ignored] [dumpDir]");
}
