using EpochSim.Execution;
using EpochSim.Hosting;
using EpochSim.Kernel.Time;
using EpochSim.Samples.Population;
using EpochSim.Serialization.EventLog;
using EpochSim.Serialization.State;

public sealed class HostingRunScopeTests
{
    [Fact]
    public void BuildRunScope_CreatesExpectedArtifacts_AndDisposesCleanly()
    {
        var root = Path.Combine(Path.GetTempPath(), $"epochsim-hosting-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var state = new WorldState();
            var engine = new SimulationEngine<WorldState>();
            engine.AddSystem(new PopulationSystem());
            engine.RegisterCommandHandler(new GrowPopulationHandler());
            engine.RegisterCommandHandler(new ScheduleFireHandler());

            var codec = new JsonEventCodecBuilder()
                .Register<PopulationDeltaEvent>()
                .Register<FireEvent>()
                .Register<FireScheduledEvent>()
                .Build();

            var serializer = new JsonStateSerializer<WorldState>();

            using var run = EpochSimRun.For(state)
                .WithRootDirectory(root)
                .WithRunId("run-1")
                .WithEventLog(codec)
                .WithSnapshots(serializer, everyTicks: 1)
                .WithStateFingerprints(serializer, everyTicks: 1)
                .WithTraceJsonl()
                .WithProfilingJsonl()
                .WithFailureArtifacts(serializer, codec, tailSize: 32)
                .Build();

            run.AttachTo(engine);
            engine.RunTicks(state, seed: 12345, start: SimTime.Zero, endInclusive: new SimTime(5), context: run.Context);

            var paths = run.Paths;
            run.Dispose();

            Assert.True(Directory.Exists(paths.RunDir));
            Assert.True(File.Exists(paths.EventsPath));
            Assert.True(File.Exists(paths.TracePath));
            Assert.True(File.Exists(paths.ProfilePath));
            Assert.True(File.Exists(paths.StateFpPath));
            Assert.True(File.Exists(paths.ManifestPath));
            Assert.True(File.Exists(paths.MetaPath));
            Assert.True(Directory.Exists(paths.SnapshotsDir));
            Assert.NotEmpty(Directory.EnumerateFiles(paths.SnapshotsDir, "snapshot-*.json"));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
