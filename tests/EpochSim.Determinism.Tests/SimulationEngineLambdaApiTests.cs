using EpochSim.Execution;
using EpochSim.Kernel.Messaging;

public sealed class SimulationEngineLambdaApiTests
{
    [Fact]
    public void LambdaSystemAndCommandHandler_RunWithoutConcreteClasses()
    {
        var state = new CounterState();
        var engine = new SimulationEngine<CounterState>();

        engine.AddSystem(
            name: "counter",
            tick: ctx =>
            {
                if (ctx.Time.Tick == 0)
                    ctx.Commands.Enqueue(new AddCommand(5));
            },
            handle: (ctx, ev) =>
            {
                if (ev is AddedEvent added)
                    ctx.State.Value += added.Delta;
            });

        engine.OnCommand<AddCommand>((_, command, events) =>
        {
            events.Emit(new AddedEvent(command.Delta));
        });

        engine.RunTicks(state, seed: 12345, endTickInclusive: 0);

        Assert.Equal(5, state.Value);
    }

    [Fact]
    public void RunTicks_LongOverloads_WorkForOffsetRange()
    {
        var state = new CounterState();
        var engine = new SimulationEngine<CounterState>();

        engine.AddSystem(
            name: "counter",
            tick: ctx =>
            {
                if (ctx.Time.Tick == 10)
                    ctx.Commands.Enqueue(new AddCommand(3));
            },
            handle: (ctx, ev) =>
            {
                if (ev is AddedEvent added)
                    ctx.State.Value += added.Delta;
            });

        engine.OnCommand<AddCommand>((_, command, events) =>
        {
            events.Emit(new AddedEvent(command.Delta));
        });

        engine.RunTicks(state, seed: 1, startTick: 10, endTickInclusive: 10);

        Assert.Equal(3, state.Value);
    }

    private sealed class CounterState
    {
        public int Value { get; set; }
    }

    [MessageKind("Add")]
    private sealed record AddCommand(int Delta) : ICommand;

    [MessageKind("Added")]
    private sealed record AddedEvent(int Delta) : IEvent;
}
