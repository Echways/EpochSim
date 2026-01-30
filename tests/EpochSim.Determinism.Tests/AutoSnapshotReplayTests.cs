using EpochSim.Execution;
using EpochSim.Execution.Middleware;
using EpochSim.Execution.Snapshots;
using EpochSim.Kernel.Time;
using EpochSim.Samples.Population;
using EpochSim.Serialization.EventLog;
using EpochSim.Serialization.State;
using Xunit;

public sealed class AutoSnapshotReplayTests
{
    [Fact]
    public void AutoSnapshots_EnableFastReplayToEnd()
    {
        var eventsPath = Path.Combine(Path.GetTempPath(), $"epochsim-events-{Guid.NewGuid():N}.jsonl");
        var snapsDir = Path.Combine(Path.GetTempPath(), $"epochsim-snaps-{Guid.NewGuid():N}");
        Directory.CreateDirectory(snapsDir);

        try
        {
            var endTick = 200L;

            var (pFull, fFull) = RunWithAutoSnapshots(eventsPath, snapsDir, endTick, snapEvery: 25);

            var engine = new SimulationEngine<WorldState>();
            engine.AddSystem(new PopulationSystem());

            var codec = new PopulationEventCodec();
            var serializer = new JsonStateSerializer<WorldState>();

            var world = SnapshotRunner.LoadBestAndReplayTo(
                engine: engine,
                snapshotsDir: snapsDir,
                eventsPath: eventsPath,
                serializer: serializer,
                codec: codec,
                seed: 12345,
                endTick: endTick,
                newState: () => new WorldState());

            Assert.Equal(pFull, world.Population);
            Assert.Equal(fFull, world.Fires);
        }
        finally
        {
            if (File.Exists(eventsPath)) File.Delete(eventsPath);
            if (Directory.Exists(snapsDir)) Directory.Delete(snapsDir, recursive: true);
        }
    }

    private static (int pop, int fires) RunWithAutoSnapshots(string eventsPath, string snapsDir, long endTick, long snapEvery)
    {
        var codec = new PopulationEventCodec();
        var serializer = new JsonStateSerializer<WorldState>();

        var engine = new SimulationEngine<WorldState>();
        engine.AddSystem(new PopulationSystem());
        engine.RegisterCommandHandler(new GrowPopulationHandler());
        engine.RegisterCommandHandler(new ScheduleFireHandler());

        using var writer = new EventLogWriter(eventsPath);
        engine.AddMiddleware(new EventLogMiddleware(writer, codec));

        var world = new WorldState();
        engine.AddMiddleware(new SnapshotMiddleware<WorldState>(world, snapEvery, snapsDir, serializer));

        engine.RunTicks(world, seed: 12345, start: SimTime.Zero, endInclusive: new SimTime(endTick));

        return (world.Population, world.Fires);
    }
}
