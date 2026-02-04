using System.Text.Json;
using System.Threading;
using EpochSim.Execution;
using EpochSim.Execution.Middleware;
using EpochSim.Kernel.Messaging;
using EpochSim.Kernel.Systems;
using EpochSim.Kernel.Time;
using EpochSim.Kernel.Validation;
using EpochSim.Serialization.EventLog;
using EpochSim.Serialization.State;
using Xunit;

public sealed class RunLifecycleTests
{
    [Fact]
    public void OnRunEnd_IsCalled_OnInvariantFailure()
    {
        var state = new TestState();
        var engine = new SimulationEngine<TestState>();
        engine.AddSystem(new NoopSystem());

        var lifecycle = new LifecycleMiddleware();
        engine.AddMiddleware(lifecycle);

        var invariants = new List<IInvariant<TestState>> { new AlwaysFailInvariant<TestState>() };
        engine.AddMiddleware(new InvariantMiddleware<TestState>(state, invariants));

        Assert.Throws<InvariantViolationException>(() =>
            engine.RunTicks(state, seed: 1, start: SimTime.Zero, endInclusive: new SimTime(0)));

        Assert.Equal(1, lifecycle.RunStartCount);
        Assert.Equal(1, lifecycle.RunFailedCount);
        Assert.Equal(1, lifecycle.RunEndCount);
    }

    [Fact]
    public void FailureArtifacts_WritesReportAndSnapshot()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"epochsim-failure-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);

        try
        {
            var state = new TestState();
            var serializer = new JsonStateSerializer<TestState>();
            var codec = new TestCodec();

            var engine = new SimulationEngine<TestState>();
            engine.AddSystem(new EmitOnTickSystem());
            engine.RegisterCommandHandler(new EmitCommandHandler());

            engine.AddMiddleware(new FailureArtifactsMiddleware<TestState>(
                state,
                serializer,
                codec,
                snapshotEnabled: true,
                tailSize: 5));

            var invariants = new List<IInvariant<TestState>> { new AlwaysFailInvariant<TestState>() };
            engine.AddMiddleware(new InvariantMiddleware<TestState>(state, invariants));

            var runContext = new RunContext("run", dir);

            Assert.Throws<InvariantViolationException>(() =>
                engine.RunTicks(
                    state,
                    seed: 1,
                    start: SimTime.Zero,
                    endInclusive: new SimTime(0),
                    context: runContext));

            var reportPath = Path.Combine(dir, "failure-report.json");
            var snapshotPath = Path.Combine(dir, "failure-snapshot.json");

            Assert.True(File.Exists(reportPath));
            Assert.True(File.Exists(snapshotPath));

            using var doc = JsonDocument.Parse(File.ReadAllText(reportPath));
            var root = doc.RootElement;

            Assert.Equal(0, root.GetProperty("Tick").GetInt64());
            var exceptionType = root.GetProperty("ExceptionType").GetString() ?? "";
            Assert.EndsWith("InvariantViolationException", exceptionType, StringComparison.Ordinal);

            var lastEvents = root.GetProperty("LastEvents");
            Assert.True(lastEvents.GetArrayLength() > 0);
            Assert.Equal("TestEvent", lastEvents[0].GetProperty("Kind").GetString());
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Cancellation_SkipsFailureArtifacts_ButCallsOnRunEnd()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"epochsim-cancel-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);

        try
        {
            var state = new TestState();
            var serializer = new JsonStateSerializer<TestState>();
            var codec = new TestCodec();
            var lifecycle = new LifecycleMiddleware();

            var engine = new SimulationEngine<TestState>();
            engine.AddSystem(new NoopSystem());
            engine.AddMiddleware(lifecycle);
            engine.AddMiddleware(new FailureArtifactsMiddleware<TestState>(state, serializer, codec, snapshotEnabled: true));

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var runContext = new RunContext("run", dir);

            Assert.Throws<OperationCanceledException>(() =>
                engine.RunTicks(
                    state,
                    seed: 1,
                    start: SimTime.Zero,
                    endInclusive: new SimTime(10),
                    context: runContext,
                    cancellationToken: cts.Token));

            Assert.Equal(1, lifecycle.RunEndCount);
            Assert.False(File.Exists(Path.Combine(dir, "failure-report.json")));
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    private sealed class LifecycleMiddleware : IExecutionMiddleware
    {
        public int RunStartCount { get; private set; }
        public int RunFailedCount { get; private set; }
        public int RunEndCount { get; private set; }

        public void OnRunStart(RunInfo info) => RunStartCount++;
        public void OnRunFailed(RunInfo info, Exception exception) => RunFailedCount++;
        public void OnRunEnd(RunInfo info) => RunEndCount++;
    }

    private sealed class TestState
    {
        public int Value { get; set; }
    }

    private sealed class NoopSystem : ISystem<TestState>
    {
        public string Name => "noop";
        public void Tick(TickContext<TestState> ctx) { }
        public void Handle(EventContext<TestState> ctx, IEvent ev) { }
    }

    private sealed class EmitOnTickSystem : ISystem<TestState>
    {
        public string Name => "emit-on-tick";

        public void Tick(TickContext<TestState> ctx)
        {
            if (ctx.Time.Tick == 0)
                ctx.Commands.Enqueue(new EmitCommand());
        }

        public void Handle(EventContext<TestState> ctx, IEvent ev) { }
    }

    private sealed class EmitCommandHandler : ICommandHandler<TestState, EmitCommand>
    {
        public void Handle(TestState state, EmitCommand command, IEventBuffer events)
            => events.Emit(new TestEvent());
    }

    private sealed record EmitCommand() : ICommand
    {
        public string Kind => "Emit";
    }

    private sealed record TestEvent() : IEvent
    {
        public string Kind => "TestEvent";
    }

    private sealed class TestCodec : IEventCodecV2
    {
        public bool TryEncode(IEvent ev, out string kind, out string payloadJson)
        {
            if (ev is not TestEvent)
            {
                kind = "";
                payloadJson = "";
                return false;
            }

            kind = "TestEvent";
            payloadJson = "\"\"";
            return true;
        }

        public bool TryDecode(string kind, string payloadJson, out IEvent ev)
        {
            if (string.Equals(kind, "TestEvent", StringComparison.Ordinal))
            {
                ev = new TestEvent();
                return true;
            }

            ev = default!;
            return false;
        }
    }

    private sealed class AlwaysFailInvariant<TState> : IInvariant<TState>
    {
        public string Name => "AlwaysFail";

        public bool Check(SimTime time, TState state, out string message)
        {
            message = "fail";
            return false;
        }
    }
}
