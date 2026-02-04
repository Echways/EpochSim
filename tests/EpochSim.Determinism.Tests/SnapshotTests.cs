using EpochSim.Execution;
using EpochSim.Execution.Middleware;
using EpochSim.Kernel.Time;
using EpochSim.Samples.Population;
using EpochSim.Serialization.EventLog;
using EpochSim.Serialization.Snapshots;
using EpochSim.Serialization.State;
using Xunit;

public sealed class SnapshotTests
{
    [Fact]
    public void SaveThenLoad_ProducesSameState()
    {
        var snapshotPath = Path.Combine(Path.GetTempPath(), $"epochsim-snap-{Guid.NewGuid():N}.json");

        try
        {
            var (savedPopulation, savedFires) = RunAndSave(snapshotPath);
            var (loadedPopulation, loadedFires) = Load(snapshotPath);

            Assert.Equal(savedPopulation, loadedPopulation);
            Assert.Equal(savedFires, loadedFires);
        }
        finally
        {
            if (File.Exists(snapshotPath)) File.Delete(snapshotPath);
        }
    }

    private static (int pop, int fires) RunAndSave(string snapPath)
    {
        var codec = new PopulationEventCodec();
        var stateSerializer = new JsonStateSerializer<WorldState>();

        var engine = new SimulationEngine<WorldState>();
        engine.AddSystem(new PopulationSystem());
        engine.RegisterCommandHandler(new GrowPopulationHandler());
        engine.RegisterCommandHandler(new ScheduleFireHandler());

        using var writer = new EventLogWriter(Path.Combine(Path.GetTempPath(), $"epochsim-events-{Guid.NewGuid():N}.jsonl"));
        engine.AddMiddleware(new EventLogMiddleware(writer, codec));

        var world = new WorldState();
        engine.RunTicks(world, seed: 12345, start: SimTime.Zero, endInclusive: new SimTime(60));

        SnapshotWriter.Write(snapPath, tick: 60, stateJson: stateSerializer.Serialize(world));
        return (world.Population, world.Fires);
    }

    private static (int pop, int fires) Load(string snapPath)
    {
        var stateSerializer = new JsonStateSerializer<WorldState>();
        var snap = SnapshotReader.Read(snapPath);
        var world = stateSerializer.Deserialize(snap.StateJson);
        return (world.Population, world.Fires);
    }
}
