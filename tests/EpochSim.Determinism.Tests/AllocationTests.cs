using EpochSim.Kernel.Messaging;
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

    private sealed record TestCommand() : ICommand
    {
        public string Kind => "TestCommand";
    }

    private sealed record TestEvent() : IEvent
    {
        public string Kind => "TestEvent";
    }
}
