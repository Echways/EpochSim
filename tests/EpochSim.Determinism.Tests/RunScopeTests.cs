using EpochSim.Execution;
using EpochSim;
using EpochSim.Execution.RunArtifacts;
using EpochSim.Kernel.Time;
using EpochSim.Samples.Population;
using EpochSim.Serialization.EventLog;
using EpochSim.Serialization.State;

public sealed class RunScopeTests
{
    [Fact]
    public void BuildRunScope_CreatesExpectedArtifacts_AndDisposesCleanly()
    {
        var root = Path.Combine(Path.GetTempPath(), $"epochsim-runscope-{Guid.NewGuid():N}");
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

            run.RunTicks(engine, seed: 12345, start: SimTime.Zero, endInclusive: new SimTime(5));

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

    [Fact]
    public void RunWith_WrongState_ThrowsWithWhyFix()
    {
        var root = Path.Combine(Path.GetTempPath(), $"epochsim-doublebind-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var correctState = new WorldState();
            var wrongState = new WorldState();
            var engine = new SimulationEngine<WorldState>();

            using var run = EpochSimRun.For(correctState)
                .WithRootDirectory(root)
                .Build();

            var ex = Assert.Throws<InvalidOperationException>(() =>
                engine.RunWith(run, wrongState, seed: 1, SimTime.Zero, new SimTime(5)));

            Assert.Contains("Why:", ex.Message);
            Assert.Contains("Fix:", ex.Message);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RunTicks_OnScope_AttachesAndRunsWithBoundState()
    {
        var root = Path.Combine(Path.GetTempPath(), $"epochsim-scope-run-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var state = new WorldState();
            var engine = new SimulationEngine<WorldState>();
            engine.AddSystem(new PopulationSystem());
            engine.RegisterCommandHandler(new GrowPopulationHandler());
            engine.RegisterCommandHandler(new ScheduleFireHandler());

            RunPaths paths;
            using (var run = EpochSimRun.For(state)
                .WithRootDirectory(root)
                .Build())
            {
                Assert.Same(state, run.State);
                paths = run.Paths;
                run.RunTicks(engine, seed: 42, endTickInclusive: 10);
            } // Dispose writes manifest

            Assert.True(File.Exists(paths.ManifestPath));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
