using EpochSim.Execution;
using EpochSim.Execution.Middleware;
using EpochSim.Execution.RunArtifacts;
using EpochSim.Execution.StateFingerprint;
using EpochSim.Kernel.Validation;
using EpochSim.Observability.Profiling;
using EpochSim.Observability.Tracing;
using EpochSim.Serialization.EventLog;
using EpochSim.Serialization.State;

namespace EpochSim.Hosting;

public sealed class EpochSimRunBuilder<TState>
{
    private readonly TState _state;
    private EpochSimRunOptions _runOptions = new();

    private IEventCodecV2? _eventCodec;

    private SnapshotRunOptions? _snapshotOptions;
    private IStateSerializer<TState>? _snapshotSerializer;

    private StateFingerprintRunOptions? _fingerprintOptions;
    private IStateSerializer<TState>? _fingerprintSerializer;

    private bool _traceEnabled;
    private bool _profilingEnabled;

    private InvariantRunOptions? _invariantOptions;
    private IReadOnlyList<IInvariant<TState>> _invariants = Array.Empty<IInvariant<TState>>();

    private IStateSerializer<TState>? _mutationGuardSerializer;

    private FailureArtifactsRunOptions? _failureOptions;
    private IStateSerializer<TState>? _failureSerializer;
    private IEventCodecV2? _failureCodec;

    internal EpochSimRunBuilder(TState state)
        => _state = state;

    public EpochSimRunBuilder<TState> WithRootDirectory(string rootDirectory)
    {
        _runOptions = CloneRunOptions(rootDirectory: rootDirectory);
        return this;
    }

    public EpochSimRunBuilder<TState> WithRunId(string runId)
    {
        _runOptions = CloneRunOptions(runId: runId);
        return this;
    }

    public EpochSimRunBuilder<TState> WithRunId(Func<string> runIdFactory)
    {
        ArgumentNullException.ThrowIfNull(runIdFactory);
        _runOptions = CloneRunOptions(runId: runIdFactory());
        return this;
    }

    public EpochSimRunBuilder<TState> WithCompression(bool enabled = true)
    {
        _runOptions = CloneRunOptions(compression: enabled);
        return this;
    }

    public EpochSimRunBuilder<TState> WithEventLog(IEventCodecV2 codec)
    {
        _eventCodec = codec ?? throw new ArgumentNullException(nameof(codec));
        return this;
    }

    public EpochSimRunBuilder<TState> WithSnapshots(IStateSerializer<TState> serializer, long everyTicks = 50)
    {
        _snapshotSerializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _snapshotOptions = new SnapshotRunOptions { EveryTicks = everyTicks };
        return this;
    }

    public EpochSimRunBuilder<TState> WithStateFingerprints(IStateSerializer<TState> serializer, long everyTicks = 1)
    {
        _fingerprintSerializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _fingerprintOptions = new StateFingerprintRunOptions { EveryTicks = everyTicks };
        return this;
    }

    public EpochSimRunBuilder<TState> WithTraceJsonl()
    {
        _traceEnabled = true;
        return this;
    }

    public EpochSimRunBuilder<TState> WithProfilingJsonl()
    {
        _profilingEnabled = true;
        return this;
    }

    public EpochSimRunBuilder<TState> WithInvariants(IEnumerable<IInvariant<TState>> invariants, long checkEveryTicks = 1)
    {
        ArgumentNullException.ThrowIfNull(invariants);
        _invariants = invariants as IReadOnlyList<IInvariant<TState>> ?? invariants.ToArray();
        _invariantOptions = new InvariantRunOptions { CheckEveryTicks = checkEveryTicks };
        return this;
    }

    public EpochSimRunBuilder<TState> WithStateMutationGuard(IStateSerializer<TState> serializer)
    {
        _mutationGuardSerializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        return this;
    }

    public EpochSimRunBuilder<TState> WithFailureArtifacts(IStateSerializer<TState> serializer, IEventCodecV2 codec, int tailSize = 200)
    {
        _failureSerializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _failureCodec = codec ?? throw new ArgumentNullException(nameof(codec));
        _failureOptions = new FailureArtifactsRunOptions { TailSize = tailSize };
        return this;
    }

    public EpochSimRunScope<TState> Build()
    {
        var paths = new RunPaths(_runOptions.RootDirectory, _runOptions.RunId);
        paths.Ensure();

        var middleware = new List<IExecutionMiddleware>();
        var resources = new List<IDisposable>();
        TraceRuntime? traceRuntime = null;
        ProfileRuntime? profileRuntime = null;

        var capture = new RunCaptureMiddleware();
        middleware.Add(capture);

        if (_traceEnabled)
        {
            var traceSink = new InMemoryTraceSink();
            middleware.Add(new TraceMiddleware(traceSink));

            var traceExporter = new DeferredTraceExport();
            var tracePath = _runOptions.Compression ? paths.TracePathGz : paths.TracePath;
            var traceWriter = new JsonlTraceWriter(tracePath);
            traceExporter?.Writer = traceWriter;
            resources.Add(traceWriter);

            traceRuntime = new TraceRuntime(traceSink, traceExporter!);
        }

        if (_profilingEnabled)
        {
            var profileSink = new InMemoryProfileSink();
            middleware.Add(new ProfilingMiddleware(profileSink));

            var profileExporter = new DeferredProfileExport();
            var profilePath = _runOptions.Compression ? paths.ProfilePathGz : paths.ProfilePath;
            var profileWriter = new JsonlProfileWriter(profilePath);
            profileExporter?.Writer = profileWriter;
            resources.Add(profileWriter);

            profileRuntime = new ProfileRuntime(profileSink, profileExporter!);
        }

        if (_eventCodec is not null)
        {
            var eventsPath = _runOptions.Compression ? paths.EventsPathGz : paths.EventsPath;
            var eventWriter = new EventLogWriter(eventsPath);
            resources.Add(eventWriter);
            middleware.Add(new EventLogMiddleware(eventWriter, _eventCodec));
        }

        if (_snapshotOptions is not null && _snapshotSerializer is not null)
            middleware.Add(new SnapshotMiddleware<TState>(_state, _snapshotOptions.EveryTicks, paths.SnapshotsDir, _snapshotSerializer));

        if (_fingerprintOptions is not null && _fingerprintSerializer is not null)
        {
            var writer = new JsonlStateFingerprintWriter(paths.StateFpPath);
            resources.Add(writer);
            middleware.Add(new StateFingerprintMiddleware<TState>(_state, _fingerprintSerializer, writer, _fingerprintOptions.EveryTicks));
        }

        if (_mutationGuardSerializer is not null)
            middleware.Add(new StateMutationGuardMiddleware<TState>(_state, _mutationGuardSerializer));

        if (_invariantOptions is not null && _invariants.Count > 0)
            middleware.Add(new InvariantMiddleware<TState>(_state, _invariants, _invariantOptions.CheckEveryTicks));

        if (_failureOptions is not null && _failureSerializer is not null && _failureCodec is not null)
        {
            var snapshotsEnabled = _snapshotOptions is not null;
            middleware.Add(new FailureArtifactsMiddleware<TState>(
                _state,
                _failureSerializer,
                _failureCodec,
                snapshotEnabled: snapshotsEnabled,
                tailSize: _failureOptions.TailSize));
        }

        var context = new RunContext(paths.RunId, paths.RunDir);
        var composite = new CompositeExecutionMiddleware(middleware);

        var init = new EpochSimRunScopeInit<TState>(
            paths,
            context,
            composite,
            capture,
            resources,
            traceRuntime,
            profileRuntime,
            _snapshotOptions?.EveryTicks ?? 0,
            _fingerprintOptions?.EveryTicks ?? 0,
            HasEventLog: _eventCodec is not null,
            Mode: RunMode.Run);

        return new EpochSimRunScope<TState>(init);
    }

    private EpochSimRunOptions CloneRunOptions(string? rootDirectory = null, string? runId = null, bool? compression = null)
    {
        return new EpochSimRunOptions
        {
            RootDirectory = rootDirectory ?? _runOptions.RootDirectory,
            RunId = runId ?? _runOptions.RunId,
            Compression = compression ?? _runOptions.Compression
        };
    }
}
