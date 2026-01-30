using EpochSim.Execution.Middleware;
using EpochSim.Kernel.Determinism;
using EpochSim.Kernel.Messaging;
using EpochSim.Kernel.Scheduling;
using EpochSim.Kernel.Systems;
using EpochSim.Kernel.Time;
using EpochSim.Serialization.EventLog;

namespace EpochSim.Execution;

public sealed class SimulationEngine<TState>
{
    private readonly List<ISystem<TState>> _systems = [];
    private readonly List<IExecutionMiddleware> _mw = [];
    private readonly ExecutionOptions _options;
    private readonly CommandRouter<TState> _commandRouter = new();

    public SimulationEngine(ExecutionOptions? options = null) => _options = options ?? new();

    public IScheduler Scheduler { get; } = new Scheduler();

    public void AddSystem(ISystem<TState> system) => _systems.Add(system);
    public void AddMiddleware(IExecutionMiddleware middleware) => _mw.Add(middleware);

    public void RegisterCommandHandler(ICommandHandler<TState> handler)
        => _commandRouter.Register(handler);

    public void RunTicks(TState state, ulong seed, SimTime start, SimTime endInclusive)
    {
        var rng = new DeterministicRng(seed);
        var commands = new CommandBuffer();

        var time = start;

        DrainScheduledAt(time, state, commands, rng);

        while (time.Tick <= endInclusive.Tick)
        {
            foreach (var m in _mw) m.OnTickStart(time);

            var tickCtx = new TickContext<TState>(time, state, Scheduler, commands, rng);

            foreach (var sys in _systems)
            {
                foreach (var m in _mw) m.OnSystemTickStart(time, sys.Name);
                sys.Tick(tickCtx);
                foreach (var m in _mw) m.OnSystemTickEnd(time, sys.Name);
            }

            var events = new EventBuffer();

            var drained = commands.Drain();
            if (drained.Count > 0)
            {
                _commandRouter.DispatchAll(state, drained, events);
            }

            var emitted = events.Drain();
            if (emitted.Count > 0)
            {
                var evtCtxNow = new EventContext<TState>(time, state, Scheduler, commands, rng);

                foreach (var ev in emitted)
                {
                    foreach (var m in _mw) m.OnEventDispatched(time, ev);
                    foreach (var sys in _systems) sys.Handle(evtCtxNow, ev);
                }
            }

            foreach (var m in _mw) m.OnTickEnd(time);

            time = time.AddTicks(1);
            DrainScheduledAt(time, state, commands, rng);
        }
    }

    public void ReplayFromLog(
        TState state,
        ulong seed,
        SimTime start,
        SimTime endInclusive,
        EventLogIndex index,
        IEventCodec codec)
    {
        var rng = new DeterministicRng(seed);
        var scheduler = new NullScheduler();
        var commands = new NullCommandBuffer();

        var time = start;

        while (time.Tick <= endInclusive.Tick)
        {
            foreach (var m in _mw) m.OnTickStart(time);

            var atTick = index.GetAtTick(time.Tick);
            if (atTick.Count > 0)
            {
                var evtCtx = new EventContext<TState>(time, state, scheduler, commands, rng);

                for (int i = 0; i < atTick.Count; i++)
                {
                    var e = atTick[i];

                    if (!codec.TryDecode(e.Kind, e.Payload, out var ev))
                        throw new InvalidOperationException($"No codec for event kind {e.Kind}");

                    foreach (var m in _mw) m.OnEventDispatched(time, ev);

                    foreach (var sys in _systems)
                        sys.Handle(evtCtx, ev);
                }
            }

            foreach (var m in _mw) m.OnTickEnd(time);

            time = time.AddTicks(1);
        }
    }

    public void ReplayFromLog(
        TState state,
        ulong seed,
        SimTime start,
        SimTime endInclusive,
        IReadOnlyList<EventLogEntry> entries,
        IEventCodec codec)
        => ReplayFromLog(state, seed, start, endInclusive, new EventLogIndex(entries), codec);

    private void DrainScheduledAt(
        in SimTime time,
        TState state,
        CommandBuffer commands,
        DeterministicRng rng)
    {
        while (true)
        {
            var nextTime = Scheduler.PeekTime();
            if (nextTime is null || nextTime.Value.Tick != time.Tick) break;

            Scheduler.TryDequeue(out var item);
            foreach (var m in _mw) m.OnEventDispatched(time, item.Event);

            var evtCtx = new EventContext<TState>(time, state, Scheduler, commands, rng);
            foreach (var sys in _systems)
                sys.Handle(evtCtx, item.Event);
        }
    }

    private sealed class NullScheduler : IScheduler
    {
        public void Schedule(SimTime time, IEvent ev) { }
        public bool TryDequeue(out ScheduledItem item) { item = default; return false; }
        public SimTime? PeekTime() => null;
    }

    private sealed class NullCommandBuffer : ICommandBuffer
    {
        public void Enqueue(ICommand command) { }
        public IReadOnlyList<ICommand> Drain() => Array.Empty<ICommand>();
    }
}
