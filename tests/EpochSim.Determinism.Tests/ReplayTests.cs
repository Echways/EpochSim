using EpochSim.Execution;
using EpochSim.Execution.Middleware;
using EpochSim.Kernel.Time;
using EpochSim.Samples.Population;
using EpochSim.Serialization.EventLog;
using Xunit;

public sealed class ReplayTests
{
    [Fact]
    public void RunThenReplay_ProducesSameState()
    {
        var path = Path.Combine(Path.GetTempPath(), $"epochsim-events-{Guid.NewGuid():N}.jsonl");

        try
        {
            var (runPopulation, runFires) = RunAndWrite(path);
            var (indexPopulation, indexFires) = ReplayWithIndex(path);
            var (streamPopulation, streamFires) = ReplayWithStream(path);

            Assert.Equal(runPopulation, indexPopulation);
            Assert.Equal(runFires, indexFires);
            Assert.Equal(runPopulation, streamPopulation);
            Assert.Equal(runFires, streamFires);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static (int pop, int fires) RunAndWrite(string path)
    {
        var codec = new PopulationEventCodec();

        var engine = new SimulationEngine<WorldState>();
        engine.AddSystem(new PopulationSystem());
        engine.RegisterCommandHandler(new GrowPopulationHandler());
        engine.RegisterCommandHandler(new ScheduleFireHandler());

        using var writer = new EventLogWriter(path);
        engine.AddMiddleware(new EventLogMiddleware(writer, codec));

        var world = new WorldState();
        engine.RunTicks(world, seed: 12345, start: SimTime.Zero, endInclusive: new SimTime(60));

        return (world.Population, world.Fires);
    }

    private static (int pop, int fires) ReplayWithIndex(string path)
    {
        var codec = new PopulationEventCodec();
        var entries = EventLogReader.ReadAll(path);
        var index = new EventLogIndex(entries);

        var engine = new SimulationEngine<WorldState>();
        engine.AddSystem(new PopulationSystem());

        var world = new WorldState();
        engine.ReplayFromLog(world, seed: 12345, start: SimTime.Zero, endInclusive: new SimTime(60), index: index, codec: codec);

        return (world.Population, world.Fires);
    }

    private static (int pop, int fires) ReplayWithStream(string path)
    {
        var codec = new PopulationEventCodec();
        var entries = EventLogReader.ReadStream(path);

        var engine = new SimulationEngine<WorldState>();
        engine.AddSystem(new PopulationSystem());

        var world = new WorldState();
        engine.ReplayFromLogStream(world, seed: 12345, start: SimTime.Zero, endInclusive: new SimTime(60), entries: entries, codec: codec);

        return (world.Population, world.Fires);
    }
}
