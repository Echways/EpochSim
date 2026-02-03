using EpochSim.Execution;
using EpochSim.Execution.Middleware;
using EpochSim.Kernel.Messaging;
using EpochSim.Kernel.Systems;
using EpochSim.Kernel.Time;
using EpochSim.Serialization.EventLog;
using EpochSim.Serialization.State;

public sealed class SimulationEngineSemanticsTests
{
    [Fact]
    public void OnTickStart_PrecedesEventDispatch_OnFirstTick()
    {
        var state = new OrderState();
        var engine = new SimulationEngine<OrderState>();
        engine.AddSystem(new EmitOnFirstTickSystem());
        engine.RegisterCommandHandler(new EmitEventCommandHandler());

        var middleware = new OrderMiddleware(state.Log);
        engine.AddMiddleware(middleware);

        engine.RunTicks(state, seed: 1, start: SimTime.Zero, endInclusive: new SimTime(0));

        var tickIndex = state.Log.IndexOf("tick-start:0");
        var eventIndex = state.Log.FindIndex(s => s.StartsWith("event:", StringComparison.Ordinal));

        Assert.True(tickIndex >= 0, "Expected OnTickStart to be recorded.");
        Assert.True(eventIndex > tickIndex, $"Expected event dispatch after OnTickStart. Log: {string.Join(",", state.Log)}");
    }

    [Fact]
    public void EndTickInclusive_DoesNotDispatchScheduledBeyondEnd()
    {
        var state = new CountState { ShouldSchedule = true };
        var engine = new SimulationEngine<CountState>();
        engine.AddSystem(new ScheduleNextTickSystem());

        engine.RunTicks(state, seed: 1, start: SimTime.Zero, endInclusive: new SimTime(0));

        Assert.Equal(0, state.EventsDispatched);
    }

    [Fact]
    public void Pump_ProcessesEventCommandEvent_SameTick()
    {
        var state = new OrderState();
        var engine = new SimulationEngine<OrderState>();
        engine.AddSystem(new PumpSystem());
        engine.RegisterCommandHandler(new StartCommandHandler());
        engine.RegisterCommandHandler(new FollowupCommandHandler());

        engine.RunTicks(state, seed: 1, start: SimTime.Zero, endInclusive: new SimTime(0));

        Assert.Equal(new[] { "A", "B" }, state.Log);
    }

    [Fact]
    public void ScheduleAt_CurrentTick_Throws()
    {
        var state = new OrderState();
        var engine = new SimulationEngine<OrderState>();
        engine.AddSystem(new ScheduleCurrentTickSystem());

        Assert.Throws<InvalidOperationException>(() =>
            engine.RunTicks(state, seed: 1, start: SimTime.Zero, endInclusive: new SimTime(0)));
    }

    [Fact]
    public void ScheduleAt_PastTick_Throws()
    {
        var state = new OrderState();
        var engine = new SimulationEngine<OrderState>();
        engine.AddSystem(new SchedulePastTickSystem());

        Assert.Throws<InvalidOperationException>(() =>
            engine.RunTicks(state, seed: 1, start: SimTime.Zero, endInclusive: new SimTime(0)));
    }

    [Fact]
    public void Emit_ImmediateEvent_DispatchedSameTick()
    {
        var state = new OrderState();
        var engine = new SimulationEngine<OrderState>();
        engine.AddSystem(new EmitDuringHandleSystem());
        engine.RegisterCommandHandler(new EmitEventCommandHandler());

        engine.RunTicks(state, seed: 1, start: SimTime.Zero, endInclusive: new SimTime(0));

        Assert.Equal(new[] { "A", "B" }, state.Log);
    }

    [Fact]
    public void StrictReplay_ThrowsOnEmit()
    {
        var state = new OrderState();
        var engine = new SimulationEngine<OrderState>();
        engine.AddSystem(new EmitDuringHandleSystem());

        var entries = new List<EventLogEntryV2>
        {
            new(0, "A", "\"\"")
        };

        var index = new EventLogIndex(entries);
        var codec = new SimpleEventCodec();

        Assert.Throws<InvalidOperationException>(() =>
            engine.ReplayFromLog(state, seed: 1, start: SimTime.Zero, endInclusive: new SimTime(0), index: index, codec: codec, strictReplay: true));
    }

    [Fact]
    public void EngineReuse_DoesNotLeakScheduledEventsAcrossRuns()
    {
        var engine = new SimulationEngine<CountState>();
        engine.AddSystem(new ScheduleNextTickSystem());

        var first = new CountState { ShouldSchedule = true };
        engine.RunTicks(first, seed: 1, start: SimTime.Zero, endInclusive: new SimTime(0));

        var second = new CountState { ShouldSchedule = false };
        engine.RunTicks(second, seed: 1, start: SimTime.Zero, endInclusive: new SimTime(1));

        Assert.Equal(0, second.EventsDispatched);
    }

    [Fact]
    public void StateMutationGuard_ThrowsOnTickMutation()
    {
        var state = new MutationState();
        var engine = new SimulationEngine<MutationState>();
        engine.AddSystem(new TickMutatingSystem());
        engine.AddMiddleware(new StateMutationGuardMiddleware<MutationState>(state, new JsonStateSerializer<MutationState>()));

        Assert.Throws<InvalidOperationException>(() =>
            engine.RunTicks(state, seed: 1, start: SimTime.Zero, endInclusive: new SimTime(0)));
    }

    [Fact]
    public void StateMutationGuard_ThrowsOnCommandHandlerMutation()
    {
        var state = new MutationState();
        var engine = new SimulationEngine<MutationState>();
        engine.AddSystem(new CommandMutatingSystem());
        engine.RegisterCommandHandler(new MutateCommandHandler());
        engine.AddMiddleware(new StateMutationGuardMiddleware<MutationState>(state, new JsonStateSerializer<MutationState>()));

        Assert.Throws<InvalidOperationException>(() =>
            engine.RunTicks(state, seed: 1, start: SimTime.Zero, endInclusive: new SimTime(0)));
    }

    private sealed class OrderState
    {
        public List<string> Log { get; } = [];
    }

    private sealed class CountState
    {
        public bool ShouldSchedule { get; set; }
        public int EventsDispatched { get; set; }
    }

    private sealed class MutationState
    {
        public int Value { get; set; }
    }

    private sealed class OrderMiddleware(List<string> log) : IExecutionMiddleware
    {
        public void OnTickStart(SimTime time)
            => log.Add($"tick-start:{time.Tick}");

        public void OnEventDispatched(SimTime time, IEvent ev)
            => log.Add($"event:{ev.Kind}");
    }

    private sealed class EmitOnFirstTickSystem : ISystem<OrderState>
    {
        public string Name => "emit-on-first";

        public void Tick(TickContext<OrderState> ctx)
        {
            if (ctx.Time.Tick == 0)
                ctx.Commands.Enqueue(new EmitEventCommand());
        }

        public void Handle(EventContext<OrderState> ctx, IEvent ev) { }
    }

    private sealed class ScheduleNextTickSystem : ISystem<CountState>
    {
        public string Name => "schedule-next";

        public void Tick(TickContext<CountState> ctx)
        {
            if (ctx.Time.Tick == 0 && ctx.State.ShouldSchedule)
                ctx.Scheduler.ScheduleNextTick(new TestEventA());
        }

        public void Handle(EventContext<CountState> ctx, IEvent ev)
        {
            if (ev is TestEventA)
                ctx.State.EventsDispatched++;
        }
    }

    private sealed class PumpSystem : ISystem<OrderState>
    {
        public string Name => "pump";

        public void Tick(TickContext<OrderState> ctx)
        {
            if (ctx.Time.Tick == 0)
                ctx.Commands.Enqueue(new StartCommand());
        }

        public void Handle(EventContext<OrderState> ctx, IEvent ev)
        {
            switch (ev)
            {
                case TestEventA:
                    ctx.State.Log.Add("A");
                    ctx.Commands.Enqueue(new FollowupCommand());
                    break;
                case TestEventB:
                    ctx.State.Log.Add("B");
                    break;
            }
        }
    }

    private sealed class EmitDuringHandleSystem : ISystem<OrderState>
    {
        public string Name => "emit-during-handle";

        public void Tick(TickContext<OrderState> ctx)
        {
            if (ctx.Time.Tick == 0)
                ctx.Commands.Enqueue(new EmitEventCommand());
        }

        public void Handle(EventContext<OrderState> ctx, IEvent ev)
        {
            switch (ev)
            {
                case TestEventA:
                    ctx.State.Log.Add("A");
                    ctx.Events.Emit(new TestEventB());
                    break;
                case TestEventB:
                    ctx.State.Log.Add("B");
                    break;
            }
        }
    }

    private sealed class ScheduleCurrentTickSystem : ISystem<OrderState>
    {
        public string Name => "schedule-now";

        public void Tick(TickContext<OrderState> ctx)
            => ctx.Scheduler.ScheduleAt(ctx.Time, new TestEventA());

        public void Handle(EventContext<OrderState> ctx, IEvent ev) { }
    }

    private sealed class SchedulePastTickSystem : ISystem<OrderState>
    {
        public string Name => "schedule-past";

        public void Tick(TickContext<OrderState> ctx)
            => ctx.Scheduler.ScheduleAt(ctx.Time.AddTicks(-1), new TestEventA());

        public void Handle(EventContext<OrderState> ctx, IEvent ev) { }
    }

    private sealed class TickMutatingSystem : ISystem<MutationState>
    {
        public string Name => "tick-mutator";

        public void Tick(TickContext<MutationState> ctx)
            => ctx.State.Value++;

        public void Handle(EventContext<MutationState> ctx, IEvent ev) { }
    }

    private sealed class CommandMutatingSystem : ISystem<MutationState>
    {
        public string Name => "command-mutator";

        public void Tick(TickContext<MutationState> ctx)
            => ctx.Commands.Enqueue(new MutateCommand());

        public void Handle(EventContext<MutationState> ctx, IEvent ev) { }
    }

    private sealed class EmitEventCommandHandler : ICommandHandler<OrderState, EmitEventCommand>
    {
        public void Handle(OrderState state, EmitEventCommand command, IEventBuffer events)
            => events.Emit(new TestEventA());
    }

    private sealed class StartCommandHandler : ICommandHandler<OrderState, StartCommand>
    {
        public void Handle(OrderState state, StartCommand command, IEventBuffer events)
            => events.Emit(new TestEventA());
    }

    private sealed class FollowupCommandHandler : ICommandHandler<OrderState, FollowupCommand>
    {
        public void Handle(OrderState state, FollowupCommand command, IEventBuffer events)
            => events.Emit(new TestEventB());
    }

    private sealed class MutateCommandHandler : ICommandHandler<MutationState, MutateCommand>
    {
        public void Handle(MutationState state, MutateCommand command, IEventBuffer events)
            => state.Value++;
    }

    private sealed record EmitEventCommand() : ICommand
    {
        public string Kind => "EmitEvent";
    }

    private sealed record StartCommand() : ICommand
    {
        public string Kind => "Start";
    }

    private sealed record FollowupCommand() : ICommand
    {
        public string Kind => "Followup";
    }

    private sealed record MutateCommand() : ICommand
    {
        public string Kind => "Mutate";
    }

    private sealed record TestEventA() : IEvent
    {
        public string Kind => "A";
    }

    private sealed record TestEventB() : IEvent
    {
        public string Kind => "B";
    }

    private sealed class SimpleEventCodec : IEventCodecV2
    {
        public bool TryEncode(IEvent ev, out string kind, out string payloadJson)
        {
            kind = ev.Kind;
            payloadJson = "\"\"";
            return true;
        }

        public bool TryDecode(string kind, string payloadJson, out IEvent ev)
        {
            ev = kind switch
            {
                "A" => new TestEventA(),
                "B" => new TestEventB(),
                _ => null!
            };

            return ev is not null;
        }
    }
}
