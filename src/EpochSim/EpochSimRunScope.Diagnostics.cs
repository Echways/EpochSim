namespace EpochSim;

public sealed partial class EpochSimRunScope<TState>
{
    private Action<string>? _diagnostics;

    internal partial EpochSimRunScope(RunScopeInit<TState> init)
    {
        _state = init.State;
        _paths = init.Paths;
        _context = init.Context;
        _middleware = init.Middleware;
        _capture = init.Capture;
        _resources = init.Resources;
        _traceRuntime = init.TraceRuntime;
        _profileRuntime = init.ProfileRuntime;
        _snapshotEveryTicks = init.SnapshotEveryTicks;
        _fingerprintEveryTicks = init.FingerprintEveryTicks;
        _hasEventLog = init.HasEventLog;
        _mode = init.Mode;
        _domain = init.Domain;

        PublishDiagnostic($"Run scope created: RunId={_paths.RunId}.");
    }

    public partial event Action<string>? Diagnostics
    {
        add => _diagnostics += value;
        remove => _diagnostics -= value;
    }

    private partial void PublishDiagnostic(string message)
        => _diagnostics?.Invoke(message);
}
