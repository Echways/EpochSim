using EpochSim.Execution;
using EpochSim.Kernel.Validation;
using EpochSim.Serialization.EventLog;
using EpochSim.Serialization.State;

namespace EpochSim.Fuzzing;

public sealed class FuzzBuilder<TState>
{
    private Func<TState>? _stateFactory;
    private Action<SimulationEngine<TState>>? _configureEngine;
    private long? _endTick;
    private IEnumerable<ulong>? _seeds;
    private IStateSerializer<TState>? _serializer;
    private Func<IEnumerable<IInvariant<TState>>>? _invariants;
    private Action<FuzzCommandPlan>? _commands;
    private bool _checkDeterminism;
    private int _parallelism = Environment.ProcessorCount;
    private bool _stopAtFirstFailure;
    private string? _artifactsRoot;
    private IEventCodecV2? _eventCodec;
    private IProgress<FuzzProgress>? _progress;
    private bool? _shrink;
    private IReadOnlyList<int>? _onlyCommands;
    private FuzzShrinkBudget _shrinkBudget = FuzzShrinkBudget.Default;
    private string? _name;
    private string? _storePath;
    private readonly List<Action<TState, StepContext>> _steps = [];
    private readonly List<(string Name, Func<TState, bool> Predicate)> _predicates = [];
    private Func<TState, object>? _projection;

    internal FuzzBuilder()
    {
    }

    public FuzzBuilder<TState> State(Func<TState> factory)
    {
        _stateFactory = factory ?? throw new ArgumentNullException(nameof(factory));
        return this;
    }

    public FuzzBuilder<TState> Engine(Action<SimulationEngine<TState>> configure)
    {
        _configureEngine = configure ?? throw new ArgumentNullException(nameof(configure));
        return this;
    }

    public FuzzBuilder<TState> Step(Action<TState, StepContext> step)
    {
        ArgumentNullException.ThrowIfNull(step);
        _steps.Add(step);
        return this;
    }

    public FuzzBuilder<TState> Invariant(string name, Func<TState, bool> predicate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(predicate);
        _predicates.Add((name, predicate));
        return this;
    }

    public FuzzBuilder<TState> Project(Func<TState, object> projection)
    {
        _projection = projection ?? throw new ArgumentNullException(nameof(projection));
        return this;
    }

    public FuzzBuilder<TState> Ticks(long endTickInclusive)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(endTickInclusive);
        _endTick = endTickInclusive;
        return this;
    }

    public FuzzBuilder<TState> Seeds(ulong fromInclusive, ulong toInclusive)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(fromInclusive, toInclusive, nameof(fromInclusive));
        _seeds = EnumerateSeeds(fromInclusive, toInclusive);
        return this;
    }

    public FuzzBuilder<TState> Seeds(IEnumerable<ulong> seeds)
    {
        _seeds = seeds ?? throw new ArgumentNullException(nameof(seeds));
        return this;
    }

    public FuzzBuilder<TState> Serializer(IStateSerializer<TState> serializer)
    {
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        return this;
    }

    public FuzzBuilder<TState> Invariants(Func<IEnumerable<IInvariant<TState>>> factory)
    {
        _invariants = factory ?? throw new ArgumentNullException(nameof(factory));
        return this;
    }

    public FuzzBuilder<TState> Commands(Action<FuzzCommandPlan> configure)
    {
        _commands = configure ?? throw new ArgumentNullException(nameof(configure));
        return this;
    }

    public FuzzBuilder<TState> CheckDeterminism(bool enabled = true)
    {
        _checkDeterminism = enabled;
        return this;
    }

    public FuzzBuilder<TState> Parallelism(int degree)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(degree, 1);
        _parallelism = degree;
        return this;
    }

    public FuzzBuilder<TState> StopAtFirstFailure(bool enabled = true)
    {
        _stopAtFirstFailure = enabled;
        return this;
    }

    public FuzzBuilder<TState> ArtifactsRoot(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        _artifactsRoot = rootDirectory;
        return this;
    }

    public FuzzBuilder<TState> EventCodec(IEventCodecV2 codec)
    {
        _eventCodec = codec ?? throw new ArgumentNullException(nameof(codec));
        return this;
    }

    public FuzzBuilder<TState> Progress(IProgress<FuzzProgress> progress)
    {
        _progress = progress ?? throw new ArgumentNullException(nameof(progress));
        return this;
    }

    public FuzzBuilder<TState> Shrink(bool enabled = true)
    {
        _shrink = enabled;
        return this;
    }

    public FuzzBuilder<TState> OnlyCommands(IReadOnlyList<int> indices)
    {
        ArgumentNullException.ThrowIfNull(indices);

        foreach (var index in indices)
            ArgumentOutOfRangeException.ThrowIfNegative(index, nameof(indices));

        _onlyCommands = indices.Distinct().Order().ToArray();
        return this;
    }

    public FuzzBuilder<TState> Name(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _name = name;
        return this;
    }

    public FuzzBuilder<TState> FailureStore(string? path = null)
    {
        if (path is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(path);

        _storePath = path ?? FuzzFailureStore.DefaultPath;
        return this;
    }

    internal FuzzBuilder<TState> WithoutFailureStore()
    {
        _storePath = null;
        return this;
    }

    internal FuzzBuilder<TState> ShrinkBudget(int maxAttempts, TimeSpan timeLimit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxAttempts);
        ArgumentOutOfRangeException.ThrowIfLessThan(timeLimit, TimeSpan.Zero);
        _shrinkBudget = new FuzzShrinkBudget(maxAttempts, timeLimit);
        return this;
    }

    internal FuzzSettings<TState> Build()
        => new(
            _stateFactory ?? throw MissingParameter(nameof(State)),
            ComposeEngineConfiguration(),
            _endTick ?? throw MissingParameter(nameof(Ticks)),
            _seeds ?? throw MissingParameter(nameof(Seeds)),
            ResolveSerializer(),
            ComposeInvariants(),
            _commands,
            _checkDeterminism,
            _parallelism,
            _stopAtFirstFailure,
            _artifactsRoot,
            _eventCodec,
            _progress,
            _shrink ?? _commands is not null,
            _onlyCommands,
            _shrinkBudget,
            _name ?? typeof(TState).FullName ?? typeof(TState).Name,
            TypeDisplayName.Of(typeof(TState)),
            _storePath is null ? null : new FuzzFailureStore(_storePath));

    private Action<SimulationEngine<TState>> ComposeEngineConfiguration()
    {
        if (_configureEngine is null && _steps.Count == 0)
            throw new InvalidOperationException("Epoch.Fuzz requires Engine(...) or Step(...) to be configured.");

        var steps = _steps.ToArray();
        var configureEngine = _configureEngine;

        return engine =>
        {
            configureEngine?.Invoke(engine);

            if (steps.Length > 0)
                engine.AddSystem("Step", new StepSystem<TState>(steps).Tick);
        };
    }

    private IStateSerializer<TState> ResolveSerializer()
    {
        if (_projection is null)
            return _serializer ?? new JsonStateSerializer<TState>();

        return _serializer is null
            ? new ProjectedStateSerializer<TState>(_projection)
            : throw new InvalidOperationException("Project(...) and Serializer(...) cannot be combined: both decide how the state is serialized.");
    }

    private Func<IEnumerable<IInvariant<TState>>>? ComposeInvariants()
    {
        if (_predicates.Count == 0)
            return _invariants;

        var factory = _invariants;
        var predicates = _predicates.ToArray();

        return () => (factory?.Invoke() ?? [])
            .Concat(predicates.Select(entry => new PredicateInvariant<TState>(entry.Name, entry.Predicate)));
    }

    private static InvalidOperationException MissingParameter(string name)
        => new($"Epoch.Fuzz requires {name}(...) to be configured.");

    private static IEnumerable<ulong> EnumerateSeeds(ulong fromInclusive, ulong toInclusive)
    {
        for (var seed = fromInclusive; ; seed++)
        {
            yield return seed;

            if (seed == toInclusive)
                yield break;
        }
    }
}
