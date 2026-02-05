namespace EpochSim.Hosting;

public sealed partial class EpochSimRunScope<TState>
{
    private Action<string>? _diagnostics;

    internal partial EpochSimRunScope(EpochSimRunScopeInit<TState> init)
    {
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
