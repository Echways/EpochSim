using System.Threading;
using EpochSim.Kernel.Determinism;
using EpochSim.Kernel.Messaging;
using EpochSim.Kernel.Scheduling;
using EpochSim.Kernel.Systems;
using EpochSim.Kernel.Time;

namespace EpochSim.Execution;

public sealed class SimulationSession<TState> : IDisposable
{
    private readonly SimulationEngine<TState> _engine;
    private readonly TState _state;
    private readonly RunOptions _options;
    private readonly SimTime _fallbackEndTick;
    private readonly RunInfo _runInfo;

    private readonly DeterministicRng _rng;
    private readonly Scheduler _scheduler;
    private readonly CommandBuffer _commands = new();
    private readonly EventBuffer _events = new();

    private SimTime? _lastRequestedEndInclusive;
    private SimTime _lastCompletedTick;
    private bool _hasCompletedTick;
    private bool _failedNotified;
    private bool _disposed;

    internal SimulationSession(
        SimulationEngine<TState> engine,
        TState state,
        ulong seed,
        SimTime start,
        SimTime? plannedEndInclusive,
        RunOptions options,
        RunContext? context)
    {
        _engine = engine;
        _state = state;
        _options = options;

        CurrentTime = start;
        _fallbackEndTick = plannedEndInclusive ?? new SimTime(long.MaxValue);

        _rng = new DeterministicRng(seed, options.RngVersion);
        _scheduler = new Scheduler(() => CurrentTime);

        _runInfo = new RunInfo(
            Mode: RunMode.Run,
            Seed: seed,
            StartTick: start,
            EndTickInclusive: _fallbackEndTick,
            RunId: context?.RunId,
            ArtifactDir: context?.ArtifactDir,
            Options: options,
            StrictReplay: false,
            RngVersion: options.RngVersion.ToString());

        _engine.NotifyRunStart(_runInfo);
    }

    public SimTime CurrentTime { get; private set; }

    public bool TickOnce(CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        var time = CurrentTime;

        try
        {
            _engine.NotifyTickStart(time);

            _engine.DrainScheduledAt(time, _scheduler, _events);

            var tickCtx = new TickContext<TState>(time, _state, _scheduler, _commands, _rng, cancellationToken);
            for (var i = 0; i < _engine.Systems.Count; i++)
            {
                var system = _engine.Systems[i];
                _engine.NotifySystemTickStart(time, system.Name);
                system.Tick(tickCtx);
                _engine.NotifySystemTickEnd(time, system.Name);
            }

            _engine.RunPump(time, _state, _scheduler, _commands, _events, _rng, _options, cancellationToken);

            _engine.NotifyTickEnd(time);

            _lastCompletedTick = time;
            _hasCompletedTick = true;
            CurrentTime = CurrentTime.AddTicks(1);
            return true;
        }
        catch (Exception ex)
        {
            NotifyFailure(ex);
            throw;
        }
    }

    public void RunUntil(SimTime endInclusive, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        _lastRequestedEndInclusive = endInclusive;

        while (CurrentTime.Tick <= endInclusive.Tick)
            TickOnce(cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        var endInfo = _runInfo with { EndTickInclusive = ResolveEndTickForInfo() };
        _engine.NotifyRunEnd(endInfo);
    }

    private SimTime ResolveEndTickForInfo()
    {
        if (_lastRequestedEndInclusive is SimTime requested)
            return requested;

        if (_hasCompletedTick)
            return _lastCompletedTick;

        return _fallbackEndTick;
    }

    private void NotifyFailure(Exception ex)
    {
        if (_failedNotified)
            return;

        _failedNotified = true;
        var failureInfo = _runInfo with { EndTickInclusive = ResolveEndTickForInfo() };
        _engine.NotifyRunFailed(failureInfo, ex);
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SimulationSession<TState>));
    }
}
