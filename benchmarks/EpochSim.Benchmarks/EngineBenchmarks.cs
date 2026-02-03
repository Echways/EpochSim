using BenchmarkDotNet.Attributes;
using EpochSim.Execution;
using EpochSim.Execution.Middleware;
using EpochSim.Kernel.Time;
using EpochSim.Samples.Population;
using EpochSim.Serialization.EventLog;

[MemoryDiagnoser]
public class EngineBenchmarks
{
    [Params(500, 2000)]
    public int EndTick { get; set; }

    private SimulationEngine<WorldState> _runEngine = null!;
    private SimulationEngine<WorldState> _replayEngine = null!;
    private List<EventLogEntryV2> _entries = null!;
    private IEventCodecV2 _codec = null!;

    [GlobalSetup]
    public void Setup()
    {
        _codec = new PopulationEventCodec();
        _runEngine = CreateEngine(withHandlers: true);
        _replayEngine = CreateEngine(withHandlers: false);
        _entries = GenerateEntries(EndTick);
    }

    [Benchmark(Description = "RunTicks throughput")]
    public void RunTicks()
    {
        var world = new WorldState();
        _runEngine.RunTicks(world, seed: 12345, start: SimTime.Zero, endInclusive: new SimTime(EndTick));
    }

    [Benchmark(Description = "ReplayFromLogStream throughput")]
    public void ReplayFromLogStream()
    {
        var world = new WorldState();
        _replayEngine.ReplayFromLogStream(
            state: world,
            seed: 12345,
            start: SimTime.Zero,
            endInclusive: new SimTime(EndTick),
            entries: _entries,
            codec: _codec);
    }

    private SimulationEngine<WorldState> CreateEngine(bool withHandlers)
    {
        var engine = new SimulationEngine<WorldState>();
        engine.AddSystem(new PopulationSystem());

        if (withHandlers)
        {
            engine.RegisterCommandHandler(new GrowPopulationHandler());
            engine.RegisterCommandHandler(new ScheduleFireHandler());
        }

        return engine;
    }

    private List<EventLogEntryV2> GenerateEntries(int endTick)
    {
        var engine = CreateEngine(withHandlers: true);
        var world = new WorldState();
        var memLog = new InMemoryEventLogMiddleware(_codec);
        engine.AddMiddleware(memLog);

        engine.RunTicks(world, seed: 12345, start: SimTime.Zero, endInclusive: new SimTime(endTick));
        return memLog.Entries.ToList();
    }
}
