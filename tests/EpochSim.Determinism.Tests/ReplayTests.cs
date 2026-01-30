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
        var tmp = Path.Combine(Path.GetTempPath(), $"epochsim-events-{Guid.NewGuid():N}.jsonl");

        try
        {
            var (p1, f1) = RunAndWrite(tmp);
            var (p2, f2) = Replay(tmp);

            Assert.Equal(p1, p2);
            Assert.Equal(f1, f2);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
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

    private static (int pop, int fires) Replay(string path)
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
}
