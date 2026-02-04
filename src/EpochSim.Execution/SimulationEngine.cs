using EpochSim.Execution.Middleware;
using EpochSim.Kernel.Determinism;
using EpochSim.Kernel.Messaging;
using EpochSim.Kernel.Scheduling;
using EpochSim.Kernel.Systems;
using EpochSim.Kernel.Time;
using EpochSim.Serialization.EventLog;
using System.Threading;

namespace EpochSim.Execution;

public sealed class SimulationEngine<TState>
{
    private readonly List<ISystem<TState>> _systems = new();
    private readonly List<IExecutionMiddleware> _middleware = new();
    private readonly CommandRouter<TState> _commandRouter = new();

    public void AddSystem(ISystem<TState> system) => _systems.Add(system);
    public void AddMiddleware(IExecutionMiddleware middleware) => _middleware.Add(middleware);

    public void RegisterCommandHandler<TCommand>(ICommandHandler<TState, TCommand> handler)
        where TCommand : ICommand
        => _commandRouter.Register(handler);

    public void RunTicks(
        TState state,
        ulong seed,
        SimTime start,
        SimTime endInclusive,
        RunOptions? options = null,
        RunContext? context = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new RunOptions();
        options.Validate();

        var rng = new DeterministicRng(seed, options.RngVersion);
        var time = start;
        var scheduler = new Scheduler(() => time);
        var runInfo = new RunInfo(
            Mode: RunMode.Run,
            Seed: seed,
            StartTick: start,
            EndTickInclusive: endInclusive,
            RunId: context?.RunId,
            ArtifactDir: context?.ArtifactDir,
            Options: options,
            StrictReplay: false,
            RngVersion: options.RngVersion.ToString());

        try
        {
            NotifyRunStart(runInfo);

            while (time.Tick <= endInclusive.Tick)
            {
                cancellationToken.ThrowIfCancellationRequested();

                NotifyTickStart(time);

                var commands = new CommandBuffer();
                var events = new EventBuffer();

                DrainScheduledAt(time, scheduler, events);

                var tickCtx = new TickContext<TState>(time, state, scheduler, commands, rng, cancellationToken);

                foreach (var sys in _systems)
                {
                    NotifySystemTickStart(time, sys.Name);
                    sys.Tick(tickCtx);
                    NotifySystemTickEnd(time, sys.Name);
                }

                RunPump(time, state, scheduler, commands, events, rng, options, cancellationToken);

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

        var rng = new DeterministicRng(seed, options.RngVersion);
        var scheduler = new NullScheduler();
        var commands = new NullCommandBuffer();
        var time = start;
        IEventEmitter events = strictReplay
            ? new StrictReplayEventEmitter(() => time)
            : new NullEventEmitter();
        var runInfo = new RunInfo(
            Mode: RunMode.Replay,
            Seed: seed,
            StartTick: start,
            EndTickInclusive: endInclusive,
            RunId: context?.RunId,
            ArtifactDir: context?.ArtifactDir,
            Options: options,
            StrictReplay: strictReplay,
            RngVersion: options.RngVersion.ToString());

        try
        {
            NotifyRunStart(runInfo);

            while (time.Tick <= endInclusive.Tick)
            {
                cancellationToken.ThrowIfCancellationRequested();

                NotifyTickStart(time);

                var atTick = index.GetAtTick(time.Tick);
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

        var rng = new DeterministicRng(seed, options.RngVersion);
        var scheduler = new NullScheduler();
        var commands = new NullCommandBuffer();
        var time = start;
        IEventEmitter events = strictReplay
            ? new StrictReplayEventEmitter(() => time)
            : new NullEventEmitter();
        var runInfo = new RunInfo(
            Mode: RunMode.Replay,
            Seed: seed,
            StartTick: start,
            EndTickInclusive: endInclusive,
            RunId: context?.RunId,
            ArtifactDir: context?.ArtifactDir,
            Options: options,
            StrictReplay: strictReplay,
            RngVersion: options.RngVersion.ToString());

        using var enumerator = entries.GetEnumerator();
        var hasEntry = enumerator.MoveNext();

        while (hasEntry && enumerator.Current.Tick < start.Tick)
            hasEntry = enumerator.MoveNext();

        try
        {
            NotifyRunStart(runInfo);

            while (time.Tick <= endInclusive.Tick)
            {
                cancellationToken.ThrowIfCancellationRequested();

                NotifyTickStart(time);

                if (hasEntry && enumerator.Current.Tick < time.Tick)
                    throw new InvalidOperationException($"Event log out of order at tick {enumerator.Current.Tick} (current={time.Tick}).");

                if (hasEntry && enumerator.Current.Tick == time.Tick)
                {
                    var evtCtx = new EventContext<TState>(time, state, scheduler, commands, events, rng, cancellationToken);

                    while (hasEntry && enumerator.Current.Tick == time.Tick)
                    {
                        var e = enumerator.Current;

                        if (!codec.TryDecode(e.Kind, e.PayloadJson, out var ev))
                            throw new InvalidOperationException($"No codec for event kind {e.Kind}");

                        NotifyEventDispatched(time, ev);
                        DispatchToSystems(evtCtx, ev);

                        hasEntry = enumerator.MoveNext();
                        if (hasEntry && enumerator.Current.Tick < time.Tick)
                            throw new InvalidOperationException($"Event log out of order at tick {enumerator.Current.Tick} (current={time.Tick}).");
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

    private void DrainScheduledAt(
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

    private void RunPump(
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

    private void NotifyRunStart(RunInfo info)
    {
        foreach (var middleware in _middleware)
            middleware.OnRunStart(info);
    }

    private void NotifyRunEnd(RunInfo info)
    {
        foreach (var middleware in _middleware)
            middleware.OnRunEnd(info);
    }

    private void NotifyRunFailed(RunInfo info, Exception exception)
    {
        foreach (var middleware in _middleware)
            middleware.OnRunFailed(info, exception);
    }

    private void NotifyTickStart(SimTime time)
    {
        foreach (var middleware in _middleware)
            middleware.OnTickStart(time);
    }

    private void NotifyTickEnd(SimTime time)
    {
        foreach (var middleware in _middleware)
            middleware.OnTickEnd(time);
    }

    private void NotifySystemTickStart(SimTime time, string systemName)
    {
        foreach (var middleware in _middleware)
            middleware.OnSystemTickStart(time, systemName);
    }

    private void NotifySystemTickEnd(SimTime time, string systemName)
    {
        foreach (var middleware in _middleware)
            middleware.OnSystemTickEnd(time, systemName);
    }

    private void NotifyCommandHandlerStart(SimTime time, string handlerName, ICommand command)
    {
        foreach (var middleware in _middleware)
            middleware.OnCommandHandlerStart(time, handlerName, command);
    }

    private void NotifyCommandHandlerEnd(SimTime time, string handlerName, ICommand command)
    {
        foreach (var middleware in _middleware)
            middleware.OnCommandHandlerEnd(time, handlerName, command);
    }

    private void NotifyEventDispatched(SimTime time, IEvent ev)
    {
        foreach (var middleware in _middleware)
            middleware.OnEventDispatched(time, ev);
    }

    private void DispatchToSystems(EventContext<TState> ctx, IEvent ev)
    {
        foreach (var sys in _systems)
            sys.Handle(ctx, ev);
    }
}
