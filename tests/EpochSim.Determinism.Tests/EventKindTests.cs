using EpochSim.Execution;
using EpochSim.Execution.Middleware;
using EpochSim.Kernel.Messaging;
using EpochSim.Kernel.Systems;
using EpochSim.Kernel.Time;
using EpochSim.Serialization.EventLog;
using Xunit;

public sealed class EventKindTests
{
    [Fact]
    public void EventKindMismatch_Throws()
    {
        var state = new TestState();
        var engine = new SimulationEngine<TestState>();
        engine.AddSystem(new EmitMismatchSystem());
        engine.RegisterCommandHandler(new EmitMismatchCommandHandler());

        var codec = new MismatchCodec();
        engine.AddMiddleware(new InMemoryEventLogMiddleware(codec));

        Assert.Throws<InvalidOperationException>(() =>
            engine.RunTicks(state, seed: 1, start: SimTime.Zero, endInclusive: new SimTime(0)));
    }

    private sealed class TestState { }

    private sealed class EmitMismatchSystem : ISystem<TestState>
    {
        public string Name => "emit-mismatch";

        public void Tick(TickContext<TestState> ctx)
            => ctx.Commands.Enqueue(new EmitMismatchCommand());

        public void Handle(EventContext<TestState> ctx, IEvent ev) { }
    }

    private sealed class EmitMismatchCommandHandler : ICommandHandler<TestState, EmitMismatchCommand>
    {
        public void Handle(TestState state, EmitMismatchCommand command, IEventBuffer events)
            => events.Emit(new MismatchEvent());
    }

    private sealed record EmitMismatchCommand() : ICommand
    {
        public string Kind => "EmitMismatch";
    }

    private sealed record MismatchEvent() : IEvent
    {
        public string Kind => "EventA";
    }

    private sealed class MismatchCodec : IEventCodecV2
    {
        public bool TryEncode(IEvent ev, out string kind, out string payloadJson)
        {
            kind = "EventB";
            payloadJson = "\"\"";
            return true;
        }

        public bool TryDecode(string kind, string payloadJson, out IEvent ev)
        {
            ev = default!;
            return false;
        }
    }
}
