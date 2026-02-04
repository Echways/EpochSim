using EpochSim.Execution;
using EpochSim.Execution.Middleware;
using EpochSim.Kernel.Time;
using EpochSim.Samples.Population;
using EpochSim.Serialization.EventLog;
using EpochSim.Serialization.Snapshots;
using EpochSim.Serialization.State;
using Xunit;

public sealed class SnapshotReplayTests
{
    [Fact]
    public void SnapshotThenReplayRemaining_ProducesSameFinalState()
    {
        var eventsPath = Path.Combine(Path.GetTempPath(), $"epochsim-events-{Guid.NewGuid():N}.jsonl");
        var snapshotPath = Path.Combine(Path.GetTempPath(), $"epochsim-snap-{Guid.NewGuid():N}.json");

        try
        {
            var endTick = 60L;
            var snapTick = 40L;

            var (fullPopulation, fullFires) = RunFull(eventsPath, endTick);
            SaveSnapshotAt(snapshotPath, snapTick);

            var (snapshotPopulation, snapshotFires) = ReplayFromSnapshot(eventsPath, snapshotPath, endTick);

            Assert.Equal(fullPopulation, snapshotPopulation);
            Assert.Equal(fullFires, snapshotFires);
        }
        finally
        {
            if (File.Exists(eventsPath)) File.Delete(eventsPath);
            if (File.Exists(snapshotPath)) File.Delete(snapshotPath);
        }
    }

    private static (int pop, int fires) RunFull(string eventsPath, long endTick)
    {
        var codec = new PopulationEventCodec();

        var engine = new SimulationEngine<WorldState>();
        engine.AddSystem(new PopulationSystem());
        engine.RegisterCommandHandler(new GrowPopulationHandler());
        engine.RegisterCommandHandler(new ScheduleFireHandler());

        using var writer = new EventLogWriter(eventsPath);
        engine.AddMiddleware(new EventLogMiddleware(writer, codec));

        var world = new WorldState();
        engine.RunTicks(world, seed: 12345, start: SimTime.Zero, endInclusive: new SimTime(endTick));

        return (world.Population, world.Fires);
    }

    private static void SaveSnapshotAt(string snapPath, long tick)
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
        engine.RunTicks(world, seed: 12345, start: SimTime.Zero, endInclusive: new SimTime(tick));

        SnapshotWriter.Write(snapPath, tick: tick, stateJson: stateSerializer.Serialize(world));
    }

    private static (int pop, int fires) ReplayFromSnapshot(string eventsPath, string snapPath, long endTick)
    {
        var codec = new PopulationEventCodec();
        var stateSerializer = new JsonStateSerializer<WorldState>();

        var snap = SnapshotReader.Read(snapPath);
        var world = stateSerializer.Deserialize(snap.StateJson);

        var entries = EventLogReader.ReadAfterTick(eventsPath, snap.Tick);
        var index = new EventLogIndex(entries);

        var engine = new SimulationEngine<WorldState>();
        engine.AddSystem(new PopulationSystem());

        engine.ReplayFromLog(world, seed: 12345, start: new SimTime(snap.Tick + 1), endInclusive: new SimTime(endTick), index: index, codec: codec);

        return (world.Population, world.Fires);
    }
}
