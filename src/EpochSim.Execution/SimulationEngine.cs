using System.Threading;
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
    private readonly List<ISystem<TState>> _systems = new();
    private readonly List<IExecutionMiddleware> _middleware = new();
    private readonly CommandRouter<TState> _commandRouter = new();

    internal IReadOnlyList<ISystem<TState>> Systems => _systems;
    internal IReadOnlyList<IExecutionMiddleware> Middleware => _middleware;
    internal CommandRouter<TState> CommandRouter => _commandRouter;

    public void AddSystem(ISystem<TState> system) => _systems.Add(system);
    public void AddSystem(
        string name,
        Action<TickContext<TState>> tick,
        Action<EventContext<TState>, IEvent>? handle = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(tick);

        _systems.Add(new LambdaSystem(name, tick, handle));
    }

    public void AddMiddleware(IExecutionMiddleware middleware) => _middleware.Add(middleware);

    public void RegisterCommandHandler<TCommand>(ICommandHandler<TState, TCommand> handler)
        where TCommand : ICommand
        => _commandRouter.Register(handler);

    public void OnCommand<TCommand>(Action<TState, TCommand, IEventBuffer> handler)
        where TCommand : ICommand
    {
        ArgumentNullException.ThrowIfNull(handler);
        _commandRouter.Register(new LambdaCommandHandler<TCommand>(handler));
    }

    public void RunTicks(
        TState state,
        ulong seed,
        long endTickInclusive,
        CancellationToken cancellationToken = default)
        => RunTicks(
            state,
            seed,
            start: SimTime.Zero,
            endInclusive: new SimTime(endTickInclusive),
            options: null,
            context: null,
            cancellationToken);

    public void RunTicks(
        TState state,
        ulong seed,
        long startTick,
        long endTickInclusive,
        CancellationToken cancellationToken = default)
        => RunTicks(
            state,
            seed,
            start: new SimTime(startTick),
            endInclusive: new SimTime(endTickInclusive),
            options: null,
            context: null,
            cancellationToken);

    public void RunTicks(
        TState state,
        ulong seed,
        SimTime start,
        SimTime endInclusive,
        RunOptions? options = null,
        RunContext? context = null,
        CancellationToken cancellationToken = default)
    {
        using var session = CreateSession(state, seed, start, endInclusive, options, context);
        session.RunUntil(endInclusive, cancellationToken);
    }

    public SimulationSession<TState> CreateSession(
        TState state,
        ulong seed,
        long startTick = 0,
        RunOptions? options = null,
        RunContext? context = null)
        => CreateSession(state, seed, new SimTime(startTick), options, context);

    public SimulationSession<TState> CreateSession(
        TState state,
        ulong seed,
        SimTime start,
        RunOptions? options = null,
        RunContext? context = null)
        => CreateSession(state, seed, start, plannedEndInclusive: null, options, context);

    internal SimulationSession<TState> CreateSession(
        TState state,
        ulong seed,
        SimTime start,
        SimTime? plannedEndInclusive,
        RunOptions? options = null,
        RunContext? context = null)
    {
        options ??= new RunOptions();
        options.Validate();
        return new SimulationSession<TState>(this, state, seed, start, plannedEndInclusive, options, context);
    }

    public void ReplayFromLog(
        TState state,
        ulong seed,
        SimTime start,
        SimTime endInclusive,
        EventLogIndex index,
        IEventCodecV2 codec,
        bool strictReplay = false,
        RunOptions? options = null,
        RunContext? context = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new RunOptions();
        options.Validate();
        var runInfo = BuildReplayRunInfo(seed, start, endInclusive, context, options, strictReplay);
        RunReplayLoop(state, start, endInclusive, codec, runInfo,
            new DeterministicRng(seed, options.RngVersion), strictReplay,
            t => index.GetAtTick(t.Tick),
            cancellationToken);
    }

    public void ReplayFromLog(
        TState state,
        ulong seed,
        SimTime start,
        SimTime endInclusive,
        IReadOnlyList<EventLogEntryV2> entries,
        IEventCodecV2 codec,
        bool strictReplay = false,
        RunOptions? options = null,
        RunContext? context = null,
        CancellationToken cancellationToken = default)
        => ReplayFromLog(state, seed, start, endInclusive, new EventLogIndex(entries), codec, strictReplay, options, context, cancellationToken);

    public void ReplayFromLogStream(
        TState state,
        ulong seed,
        SimTime start,
        SimTime endInclusive,
        IEnumerable<EventLogEntryV2> entries,
        IEventCodecV2 codec,
        bool strictReplay = false,
        RunOptions? options = null,
        RunContext? context = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new RunOptions();
        options.Validate();
        var runInfo = BuildReplayRunInfo(seed, start, endInclusive, context, options, strictReplay);

        using var enumerator = entries.GetEnumerator();
        var hasEntry = enumerator.MoveNext();
        while (hasEntry && enumerator.Current.Tick < start.Tick)
            hasEntry = enumerator.MoveNext();

        var tickBuffer = new List<EventLogEntryV2>();

        RunReplayLoop(state, start, endInclusive, codec, runInfo,
            new DeterministicRng(seed, options.RngVersion), strictReplay,
            time =>
            {
                if (hasEntry && enumerator.Current.Tick < time.Tick)
                    throw new InvalidOperationException(
                        $"Event log out of order at tick {enumerator.Current.Tick} (current={time.Tick}).");

                if (!hasEntry || enumerator.Current.Tick != time.Tick)
                    return Array.Empty<EventLogEntryV2>();

                tickBuffer.Clear();
                while (hasEntry && enumerator.Current.Tick == time.Tick)
                {
                    tickBuffer.Add(enumerator.Current);
                    hasEntry = enumerator.MoveNext();
                    if (hasEntry && enumerator.Current.Tick < time.Tick)
                        throw new InvalidOperationException(
                            $"Event log out of order at tick {enumerator.Current.Tick} (current={time.Tick}).");
                }
                return tickBuffer;
            },
            cancellationToken);
    }

    private void RunReplayLoop(
        TState state,
        SimTime start,
        SimTime endInclusive,
        IEventCodecV2 codec,
        RunInfo runInfo,
        DeterministicRng rng,
        bool strictReplay,
        Func<SimTime, IReadOnlyList<EventLogEntryV2>> getEventsForTick,
        CancellationToken cancellationToken)
    {
        var scheduler = new NullScheduler();
        var commands = new NullCommandBuffer();
        var time = start;
        IEventEmitter events = strictReplay
            ? new StrictReplayEventEmitter(() => time)
            : new NullEventEmitter();

        try
        {
            NotifyRunStart(runInfo);

            while (time.Tick <= endInclusive.Tick)
            {
                cancellationToken.ThrowIfCancellationRequested();
                NotifyTickStart(time);

                var atTick = getEventsForTick(time);
                if (atTick.Count > 0)
                {
                    var evtCtx = new EventContext<TState>(time, state, scheduler, commands, events, rng, cancellationToken);
                    for (int i = 0; i < atTick.Count; i++)
                    {
                        var e = atTick[i];
                        if (!codec.TryDecode(e.Kind, e.PayloadJson, out var ev))
                            throw new InvalidOperationException($"No codec for event kind {e.Kind}");
                        NotifyEventDispatched(time, ev);
                        DispatchToSystems(evtCtx, ev);
                    }
                }

                NotifyTickEnd(time);
                time = time.AddTicks(1);
            }
        }
        catch (Exception ex)
        {
            NotifyRunFailed(runInfo, ex);
            throw;
        }
        finally
        {
            NotifyRunEnd(runInfo);
        }
    }

    private static RunInfo BuildReplayRunInfo(
        ulong seed, SimTime start, SimTime endInclusive,
        RunContext? context, RunOptions options, bool strictReplay)
        => new(
            Mode: RunMode.Replay,
            Seed: seed,
            StartTick: start,
            EndTickInclusive: endInclusive,
            RunId: context?.RunId,
            ArtifactDir: context?.ArtifactDir,
            Options: options,
            StrictReplay: strictReplay,
            RngVersion: options.RngVersion.ToString());

    internal void DrainScheduledAt(
        in SimTime time,
        Scheduler scheduler,
        IEventBuffer events)
    {
        while (true)
        {
            var nextTime = scheduler.PeekTime();
            if (nextTime is null || nextTime.Value.Tick != time.Tick) break;

            scheduler.TryDequeue(out var item);
            events.Emit(item.Event);
        }
    }

    private sealed class NullScheduler : IScheduler
    {
        public void ScheduleAt(SimTime time, IEvent ev) { }
        public void ScheduleNextTick(IEvent ev) { }
        public void ScheduleInTicks(long deltaTicks, IEvent ev) { }
    }

    private sealed class NullCommandBuffer : ICommandBuffer
    {
        public void Enqueue(ICommand command) { }
        public IReadOnlyList<ICommand> Drain() => Array.Empty<ICommand>();
    }

    private sealed class NullEventEmitter : IEventEmitter
    {
        public void Emit(IEvent ev) { }
    }

    private sealed class StrictReplayEventEmitter(Func<SimTime> currentTimeProvider) : IEventEmitter
    {
        public void Emit(IEvent ev)
        {
            var time = currentTimeProvider();
            throw new InvalidOperationException($"Event emission during replay is not allowed (tick={time.Tick}, event={ev.GetType().Name}).");
        }
    }

    private sealed class LambdaSystem(
        string name,
        Action<TickContext<TState>> tick,
        Action<EventContext<TState>, IEvent>? handle) : ISystem<TState>
    {
        private readonly Action<EventContext<TState>, IEvent> _handle = handle ?? ((_, _) => { });
        public string Name => name;
        public void Tick(TickContext<TState> ctx) => tick(ctx);
        public void Handle(EventContext<TState> ctx, IEvent ev) => _handle(ctx, ev);
    }

    private sealed class LambdaCommandHandler<TCommand>(
        Action<TState, TCommand, IEventBuffer> handler) : ICommandHandler<TState, TCommand>
        where TCommand : ICommand
    {
        public void Handle(TState state, TCommand command, IEventBuffer events)
            => handler(state, command, events);
    }

    internal void RunPump(
        SimTime time,
        TState state,
        IScheduler scheduler,
        CommandBuffer commands,
        EventBuffer events,
        DeterministicRng rng,
        RunOptions options,
        CancellationToken cancellationToken)
    {
        var pumpSteps = 0;
        var eventsDispatched = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            pumpSteps++;
            if (pumpSteps > options.MaxPumpStepsPerTick)
                throw new InvalidOperationException(
                    $"Tick {time.Tick} exceeded MaxPumpStepsPerTick={options.MaxPumpStepsPerTick} (steps={pumpSteps}, eventsDispatched={eventsDispatched}).");

            var drainedCommands = commands.Drain();
            var hadCommands = drainedCommands.Count > 0;
            if (hadCommands)
            {
                _commandRouter.DispatchAll(
                    state,
                    drainedCommands,
                    events,
                    before: (name, cmd) => NotifyCommandHandlerStart(time, name, cmd),
                    after: (name, cmd) => NotifyCommandHandlerEnd(time, name, cmd));
                if (drainedCommands is List<ICommand> commandList)
                    commandList.Clear();
            }

            var drainedEvents = events.Drain();
            var hadEvents = drainedEvents.Count > 0;
            if (hadEvents)
            {
                var evtCtxNow = new EventContext<TState>(time, state, scheduler, commands, events, rng, cancellationToken);

                foreach (var ev in drainedEvents)
                {
                    NotifyEventDispatched(time, ev);
                    eventsDispatched++;
                    if (eventsDispatched > options.MaxEventsPerTick)
                        throw new InvalidOperationException(
                            $"Tick {time.Tick} exceeded MaxEventsPerTick={options.MaxEventsPerTick} (eventsDispatched={eventsDispatched}, pumpSteps={pumpSteps}).");

                    DispatchToSystems(evtCtxNow, ev);
                }

                if (drainedEvents is List<IEvent> eventList)
                    eventList.Clear();
            }

            if (!hadCommands && !hadEvents)
                break;
        }
    }

    internal void NotifyRunStart(RunInfo info)
    {
        foreach (var middleware in _middleware)
            middleware.OnRunStart(info);
    }

    internal void NotifyRunEnd(RunInfo info)
    {
        foreach (var middleware in _middleware)
            middleware.OnRunEnd(info);
    }

    internal void NotifyRunFailed(RunInfo info, Exception exception)
    {
        foreach (var middleware in _middleware)
            middleware.OnRunFailed(info, exception);
    }

    internal void NotifyTickStart(SimTime time)
    {
        foreach (var middleware in _middleware)
            middleware.OnTickStart(time);
    }

    internal void NotifyTickEnd(SimTime time)
    {
        foreach (var middleware in _middleware)
            middleware.OnTickEnd(time);
    }

    internal void NotifySystemTickStart(SimTime time, string systemName)
    {
        foreach (var middleware in _middleware)
            middleware.OnSystemTickStart(time, systemName);
    }

    internal void NotifySystemTickEnd(SimTime time, string systemName)
    {
        foreach (var middleware in _middleware)
            middleware.OnSystemTickEnd(time, systemName);
    }

    internal void NotifyCommandHandlerStart(SimTime time, string handlerName, ICommand command)
    {
        foreach (var middleware in _middleware)
            middleware.OnCommandHandlerStart(time, handlerName, command);
    }

    internal void NotifyCommandHandlerEnd(SimTime time, string handlerName, ICommand command)
    {
        foreach (var middleware in _middleware)
            middleware.OnCommandHandlerEnd(time, handlerName, command);
    }

    internal void NotifyEventDispatched(SimTime time, IEvent ev)
    {
        foreach (var middleware in _middleware)
            middleware.OnEventDispatched(time, ev);
    }

    internal void DispatchToSystems(EventContext<TState> ctx, IEvent ev)
    {
        foreach (var sys in _systems)
            sys.Handle(ctx, ev);
    }
}
