using EpochSim;
using EpochSim.Kernel.Messaging;

public sealed class CommandInboxTests
{
    [Fact]
    public void ScheduledCommandInbox_DrainsByTickThenInsertionOrder()
    {
        var state = new InboxState();
        var engine = Epoch.CreateEngine<InboxState>();
        var inbox = new ScheduledCommandInbox();

        engine.AttachScheduledInbox(inbox);
        engine.OnCommand<AppendValue>((s, cmd, _) => s.Values.Add(cmd.Value));
        engine.AddSystem("NoOp", tick: _ => { });

        inbox.Enqueue(2, new AppendValue(20));
        inbox.Enqueue(1, new AppendValue(10));
        inbox.Enqueue(2, new AppendValue(21));
        inbox.Enqueue(0, new AppendValue(0));
        inbox.Enqueue(2, new AppendValue(22));

        engine.RunTicks(state, seed: 1, endTickInclusive: 3);

        Assert.Equal([0, 10, 20, 21, 22], state.Values);
    }

    [Fact]
    public void CommandInbox_DrainsIntoSameTickPump()
    {
        var state = new InboxState();
        var engine = Epoch.CreateEngine<InboxState>();
        var inbox = new CommandInbox();

        engine.AttachInbox(inbox);
        engine.OnCommand<AppendValue>((s, cmd, _) => s.Values.Add(cmd.Value));
        engine.AddSystem("NoOp", tick: _ => { });

        inbox.Enqueue(new AppendValue(7));
        inbox.Enqueue(new AppendValue(8));

        engine.RunTicks(state, seed: 1, endTickInclusive: 0);

        Assert.Equal([7, 8], state.Values);
        Assert.Equal(0, inbox.Count);
    }

    private sealed class InboxState
    {
        public List<int> Values { get; } = [];
    }

    [MessageKind("AppendValue")]
    private sealed record AppendValue(int Value) : ICommand;
}
