using EpochSim.Execution;
using EpochSim.Kernel.Messaging;
using EpochSim.Kernel.Systems;
using EpochSim.Kernel.Time;
using Xunit;

public sealed class AllocationTests
{
    [Fact]
    public void CommandBuffer_Drain_HasLowAllocations()
    {
        var buffer = new CommandBuffer();
        var cmd = new TestCommand();

        for (int i = 0; i < 100; i++)
            DrainCommandBuffer(buffer, cmd);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
            DrainCommandBuffer(buffer, cmd);
        var after = GC.GetAllocatedBytesForCurrentThread();

        var allocated = after - before;
        Assert.InRange(allocated, 0, 50_000);
    }

    [Fact]
    public void EventBuffer_Drain_HasLowAllocations()
    {
        var buffer = new EventBuffer();
        var ev = new TestEvent();

        for (int i = 0; i < 100; i++)
            DrainEventBuffer(buffer, ev);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
            DrainEventBuffer(buffer, ev);
        var after = GC.GetAllocatedBytesForCurrentThread();

        var allocated = after - before;
        Assert.InRange(allocated, 0, 50_000);
    }

    private static void DrainCommandBuffer(CommandBuffer buffer, ICommand command)
    {
        buffer.Enqueue(command);
        var drained = buffer.Drain();
        foreach (var _ in drained) { }
        if (drained is List<ICommand> list)
            list.Clear();
    }

    private static void DrainEventBuffer(EventBuffer buffer, IEvent ev)
    {
        buffer.Emit(ev);
        var drained = buffer.Drain();
        foreach (var _ in drained) { }
        if (drained is List<IEvent> list)
            list.Clear();
    }

    [Fact]
    public void RunPump_CachedDelegates_DoNotAllocatePerTick()
    {
        // Verifies that before/after lambdas are reused across ticks, not re-created each pump step.
        var state = new PumpState();
        var engine = new SimulationEngine<PumpState>();
        engine.AddSystem(new EnqueueCommandSystem());
        engine.RegisterCommandHandler(new NoOpCommandHandler());

        // Warmup — let the engine allocate once and cache the delegates.
        engine.RunTicks(state, seed: 1, start: SimTime.Zero, endInclusive: new SimTime(9));

        var before = GC.GetAllocatedBytesForCurrentThread();
        engine.RunTicks(state, seed: 1, start: new SimTime(10), endInclusive: new SimTime(109));
        var after = GC.GetAllocatedBytesForCurrentThread();

        var allocated = after - before;
        Assert.True(allocated < 100_000,
            $"Expected <100 KB allocated for 100 ticks with command dispatch, got {allocated} bytes.");
    }

    private sealed class PumpState { }

    private sealed class EnqueueCommandSystem : ISystem<PumpState>
    {
        public void Tick(TickContext<PumpState> ctx)
            => ctx.Commands.Enqueue(new PumpCommand());
    }

    private sealed class NoOpCommandHandler : ICommandHandler<PumpState, PumpCommand>
    {
        public void Handle(PumpState state, PumpCommand command, IEventBuffer events) { }
    }

    private sealed record PumpCommand() : ICommand
    {
        public string Kind => "PumpCommand";
    }

    private sealed record TestCommand() : ICommand
    {
        public string Kind => "TestCommand";
    }

    private sealed record TestEvent() : IEvent
    {
        public string Kind => "TestEvent";
    }
}
