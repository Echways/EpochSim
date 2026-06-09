using System.Threading;
using EpochSim.Execution;
using EpochSim.Execution.Middleware;
using EpochSim.Execution.RunArtifacts;
using EpochSim.Kernel.Time;

namespace EpochSim;

public sealed partial class EpochSimRunScope<TState> : IDisposable
{
    private readonly TState _state;
    private readonly RunPaths _paths;
    private readonly RunContext _context;
    private readonly IExecutionMiddleware _middleware;
    private readonly RunCaptureMiddleware _capture;
    private readonly IReadOnlyList<IDisposable> _resources;
    private readonly TraceRuntime? _traceRuntime;
    private readonly ProfileRuntime? _profileRuntime;
    private readonly long _snapshotEveryTicks;
    private readonly long _fingerprintEveryTicks;
    private readonly bool _hasEventLog;
    private readonly RunMode _mode;

    private bool _attached;
    private bool _disposed;

    internal partial EpochSimRunScope(RunScopeInit<TState> init);
    public partial event Action<string>? Diagnostics;

    public string RunId => _paths.RunId;
    public RunPaths Paths => _paths;
    public RunContext Context => _context;

    public TState State => _state;

    public void AttachTo(SimulationEngine<TState> engine)
    {
        ArgumentNullException.ThrowIfNull(engine);

        if (_attached)
            throw new InvalidOperationException("This run scope is already attached to an engine.");

        engine.AddMiddleware(_middleware);
        _attached = true;
        PublishDiagnostic($"Run scope attached to {engine.GetType().Name}.");
    }

    public void RunTicks(
        SimulationEngine<TState> engine,
        ulong seed,
        long endTickInclusive,
        RunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(engine);
        AttachTo(engine);
        engine.RunTicks(_state, seed, SimTime.Zero, new SimTime(endTickInclusive), options, _context, cancellationToken);
    }

    public void RunTicks(
        SimulationEngine<TState> engine,
        ulong seed,
        SimTime start,
        SimTime endInclusive,
        RunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(engine);
        AttachTo(engine);
        engine.RunTicks(_state, seed, start, endInclusive, options, _context, cancellationToken);
    }

    public SimulationSession<TState> CreateSession(
        SimulationEngine<TState> engine,
        ulong seed,
        long startTick = 0,
        RunOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(engine);
        AttachTo(engine);
        return engine.CreateSession(_state, seed, new SimTime(startTick), options, _context);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            _traceRuntime?.FlushToWriter();
            _profileRuntime?.FlushToWriter();
            WriteRunManifestAndMeta();
            PublishDiagnostic($"Run scope disposed for RunId={RunId}.");
        }
        finally
        {
            for (var i = _resources.Count - 1; i >= 0; i--)
                _resources[i].Dispose();
        }
    }

    internal IExecutionMiddleware Middleware => _middleware;

    private void WriteRunManifestAndMeta()
    {
        var info = _capture.EndedInfo ?? _capture.StartedInfo;

        var seed = info?.Seed ?? 0UL;
        var endTick = _capture.LastTick >= 0
            ? _capture.LastTick
            : info?.EndTickInclusive.Tick ?? 0L;

        var modeText = (info?.Mode ?? _mode).ToString().ToLowerInvariant();

        RunMetaWriter.Write(
            _paths,
            mode: modeText,
            seed: seed,
            endTick: endTick,
            snapEvery: _snapshotEveryTicks,
            fingerprintEvery: _fingerprintEveryTicks);

        var options = info?.Options ?? new RunOptions();
        var startTick = info?.StartTick.Tick ?? 0L;

        var manifest = new RunManifest(
            EngineVersion: RunManifestWriter.GetEngineVersion(),
            RunMode: (info?.Mode ?? _mode).ToString(),
            Seed: seed,
            StartTick: startTick,
            EndTick: endTick,
            EventLogVersion: _hasEventLog ? 2 : 0,
            RngVersion: options.RngVersion.ToString(),
            SnapshotEvery: _snapshotEveryTicks,
            FingerprintEvery: _fingerprintEveryTicks,
            MaxPumpStepsPerTick: options.MaxPumpStepsPerTick,
            MaxEventsPerTick: options.MaxEventsPerTick,
            StrictReplay: info?.StrictReplay ?? false,
            BuildTimestampUtc: DateTime.UtcNow);

        RunManifestWriter.Write(_paths.ManifestPath, manifest);
    }

    private partial void PublishDiagnostic(string message);
}
