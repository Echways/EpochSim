using EpochSim;
using EpochSim.Cli.App;
using EpochSim.Cli.Domain;
using EpochSim.Execution;
using EpochSim.Execution.RunArtifacts;
using EpochSim.Kernel.Time;
using EpochSim.Samples.Population;
using EpochSim.Serialization.EventLog;
using EpochSim.Serialization.State;
using Xunit;

public sealed class DomainAdapterTests
{
    [Fact]
    public async Task ListAdapters_PrintsKnownAdapters()
    {
        var app = new CliApp();
        var rc = await app.RunAsync(["list-adapters", "artifacts"]);
        Assert.Equal(0, rc);
    }

    [Fact]
    public void DomainAdapterRegistry_Resolve_UnknownName_ThrowsWithKnownList()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            DomainAdapterRegistry.Resolve("nonexistent-xyz"));

        Assert.Contains("nonexistent-xyz", ex.Message);
        Assert.Contains("population", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RunManifest_StoresDomain_WhenBuilderHasWithDomain()
    {
        var root = Path.Combine(Path.GetTempPath(), $"epochsim-domain-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var state = new WorldState();
            var engine = new SimulationEngine<WorldState>();
            engine.AddSystem(new PopulationSystem());
            engine.RegisterCommandHandler(new GrowPopulationHandler());
            engine.RegisterCommandHandler(new ScheduleFireHandler());

            var serializer = new JsonStateSerializer<WorldState>();
            var codec = new JsonEventCodecBuilder()
                .Register<PopulationDeltaEvent>()
                .Register<FireEvent>()
                .Register<FireScheduledEvent>()
                .Build();

            RunPaths paths;
            using (var run = EpochSimRun.For(state)
                .WithRootDirectory(root)
                .WithRunId("run-domain")
                .WithDomain("population")
                .WithEventLog(codec)
                .Build())
            {
                paths = run.Paths;
                run.RunTicks(engine, seed: 1, endTickInclusive: 5);
            }

            var manifest = RunManifestReader.TryRead(paths.ManifestPath);
            Assert.NotNull(manifest);
            Assert.Equal("population", manifest.Domain);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task FastReplay_WithDomainMismatch_Returns1WithClearError()
    {
        var root = Path.Combine(Path.GetTempPath(), $"epochsim-mismatch-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            // Create a run tagged with domain "other-domain"
            var state = new WorldState();
            var engine = new SimulationEngine<WorldState>();
            engine.AddSystem(new PopulationSystem());
            engine.RegisterCommandHandler(new GrowPopulationHandler());
            engine.RegisterCommandHandler(new ScheduleFireHandler());

            var serializer = new JsonStateSerializer<WorldState>();
            var codec = new JsonEventCodecBuilder()
                .Register<PopulationDeltaEvent>()
                .Register<FireEvent>()
                .Register<FireScheduledEvent>()
                .Build();

            using (var run = EpochSimRun.For(state)
                .WithRootDirectory(root)
                .WithRunId("run-mismatch")
                .WithDomain("other-domain")
                .WithEventLog(codec)
                .Build())
            {
                run.RunTicks(engine, seed: 1, endTickInclusive: 5);
            }

            // Now try fast-replay using the default "population" adapter
            var app = new CliApp();
            var rc = await app.RunAsync(["fast-replay", root, "run-mismatch"]);
            Assert.Equal(1, rc);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
