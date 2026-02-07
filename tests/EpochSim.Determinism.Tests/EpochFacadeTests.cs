using EpochSim;
using EpochSim.Kernel.Messaging;
using EpochSim.Serialization.EventLog;

public sealed class EpochFacadeTests
{
    [Fact]
    public void RecommendedRun_WritesCoreArtifacts()
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
