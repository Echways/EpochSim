using EpochSim;
using EpochSim.Execution.RunArtifacts;
using EpochSim.Kernel.Messaging;
using EpochSim.Serialization.EventLog;

public sealed class EpochFacadeTests
{
    [Fact]
    public void QuickRun_WithoutCodec_WritesFingerprintAndRunMeta()
    {
        var root = Path.Combine(Path.GetTempPath(), $"epochsim-facade-quick-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var state = new FacadeState();
            var engine = Epoch.CreateEngine<FacadeState>();
            engine.AddSystem("quick-system", tick: ctx => ctx.State.Value++);

            RunPaths paths;
            using (var run = Epoch.QuickRun(state, rootDir: root))
            {
                paths = run.Paths;
                engine.Attach(run);
                engine.RunTicks(state, seed: 1, endTickInclusive: 60);
            }

            Assert.Equal(61, state.Value);
            Assert.True(File.Exists(paths.StateFpPath));
            Assert.True(File.Exists(paths.MetaPath));
            Assert.True(File.Exists(paths.ManifestPath));
            Assert.True(File.Exists(paths.TracePath) || File.Exists(paths.TracePathGz));
            Assert.True(Directory.EnumerateFiles(paths.SnapshotsDir, "snapshot-*.json").Any());
            Assert.False(File.Exists(paths.EventsPath));
            Assert.False(File.Exists(paths.EventsPathGz));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RecommendedRun_WithoutCodec_WritesSnapshotsAndDiagnostics()
    {
        var root = Path.Combine(Path.GetTempPath(), $"epochsim-facade-recommended-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var state = new FacadeState();
            var engine = Epoch.CreateEngine<FacadeState>();
            engine.AddSystem("recommended-system", tick: ctx => ctx.State.Value++);

            RunPaths paths;
            using (var run = Epoch.RecommendedRun(state, rootDir: root))
            {
                paths = run.Paths;
                engine.Attach(run);
                engine.RunTicks(state, seed: 1, endTickInclusive: 60);
            }

            Assert.Equal(61, state.Value);
            Assert.True(File.Exists(paths.StateFpPath));
            Assert.True(File.Exists(paths.MetaPath));
            Assert.True(File.Exists(paths.ManifestPath));
            Assert.True(File.Exists(paths.TracePath) || File.Exists(paths.TracePathGz));
            Assert.True(File.Exists(paths.ProfilePath) || File.Exists(paths.ProfilePathGz));
            Assert.True(Directory.EnumerateFiles(paths.SnapshotsDir, "snapshot-*.json").Any());
            Assert.False(File.Exists(paths.EventsPath));
            Assert.False(File.Exists(paths.EventsPathGz));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RecommendedRun_WithCodec_WritesCoreArtifacts()
    {
        var root = Path.Combine(Path.GetTempPath(), $"epochsim-facade-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var state = new FacadeState();
            var engine = Epoch.CreateEngine<FacadeState>();

            engine.AddSystem(
                name: "facade-system",
                tick: ctx =>
                {
                    if (ctx.Time.Tick == 0)
                        ctx.Commands.Enqueue(new FacadeCommand(2));
                },
                handle: (ctx, ev) =>
                {
                    if (ev is FacadeEvent changed)
                        ctx.State.Value += changed.Delta;
                });

            engine.OnCommand<FacadeCommand>((_, command, events) =>
            {
                events.Emit(new FacadeEvent(command.Delta));
            });

            var codec = new JsonEventCodecBuilder()
                .Register<FacadeEvent>()
                .Build();

            var serializer = Epoch.JsonStateSerializer<FacadeState>();

            using (var run = Epoch.RecommendedRun(state, codec, serializer, rootDir: root))
            {
                run.AttachTo(engine);
                engine.RunTicks(state, seed: 12345, endTickInclusive: 5);

                Assert.Equal(2, state.Value);
                Assert.True(File.Exists(run.Paths.EventsPathGz));
                Assert.True(File.Exists(run.Paths.TracePathGz));
                Assert.True(File.Exists(run.Paths.ProfilePathGz));
                Assert.True(File.Exists(run.Paths.StateFpPath));
            }

            var runId = Directory
                .EnumerateDirectories(root)
                .Select(Path.GetFileName)
                .First();

            var paths = new RunPaths(root, runId!);
            Assert.True(File.Exists(paths.MetaPath));
            Assert.True(File.Exists(paths.ManifestPath));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void JsonCodecFromAssembly_FilteredMarker_Works()
    {
        var codec = Epoch.JsonCodecFromAssembly<EpochFacadeTests>(
            filter: type => type == typeof(FacadeEvent),
            strictUnknownKinds: true);

        Assert.True(codec.TryEncode(new FacadeEvent(7), out var kind, out var payload));
        Assert.Equal("FacadeEvent", kind);

        Assert.True(codec.TryDecode(kind, payload, out var decoded));
        var typed = Assert.IsType<FacadeEvent>(decoded);
        Assert.Equal(7, typed.Delta);
    }

    private sealed class FacadeState
    {
        public int Value { get; set; }
    }

    [MessageKind("FacadeCommand")]
    private sealed record FacadeCommand(int Delta) : ICommand;

    [MessageKind("FacadeEvent")]
    private sealed record FacadeEvent(int Delta) : IEvent;
}
