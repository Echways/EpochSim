using EpochSim.Execution;
using EpochSim.Execution.Middleware;
using EpochSim.Execution.StateFingerprint;
using EpochSim.Kernel.Time;
using EpochSim.Kernel.Validation;
using EpochSim.Serialization.State;

namespace EpochSim.Fuzzing;

internal sealed class FuzzSeedRunner<TState>(FuzzSettings<TState> settings)
{
    public FuzzFailure? Check(ulong seed)
    {
        var first = Execute(seed, settings.EndTick, recordFingerprints: settings.CheckDeterminism, settings.OnlyCommands);

        if (!settings.CheckDeterminism)
            return ShrinkIfNeeded(seed, first);

        var second = Execute(seed, settings.EndTick, recordFingerprints: true, settings.OnlyCommands);

        var divergenceTick = FindDivergenceTick(first, second);
        return divergenceTick is { } tick
            ? BuildNonDeterminismFailure(seed, tick)
            : ShrinkIfNeeded(seed, first);
    }

    public FuzzRunResult<TState> Trace(ulong seed)
        => Execute(seed, settings.EndTick, recordFingerprints: true, settings.OnlyCommands);

    public string WriteArtifacts(FuzzFailure failure)
    {
        var root = settings.ArtifactsRoot ?? throw new InvalidOperationException("ArtifactsRoot is not configured.");
        var runId = $"fuzz-{failure.Seed}";
        var runDir = Path.Combine(root, runId);
        if (Directory.Exists(runDir))
            Directory.Delete(runDir, recursive: true);

        var state = settings.StateFactory();
        var invariants = CreateInvariants();
        var builder = EpochSimRun.For(state)
            .WithRootDirectory(root)
            .WithRunId(runId);

        if (settings.EventCodec is { } codec)
            builder.WithRecommendedDefaults(codec, settings.Serializer, invariants);
        else
            builder.WithRecommendedDefaults(settings.Serializer, invariants);

        builder.WithStateFingerprints(settings.Serializer, everyTicks: 1);

        using var scope = builder.Build();
        var engine = CreateEngine(failure.Seed, failure.MinimalCommandIndices ?? settings.OnlyCommands);

        try
        {
            scope.RunTicks(engine, failure.Seed, failure.Tick);
        }
        catch (Exception ex) when (IsExpectedFailure(ex, failure))
        {
        }

        return scope.Paths.RunDir;
    }

    private static bool IsExpectedFailure(Exception exception, FuzzFailure failure)
        => exception is not OperationCanceledException
           && string.Equals(Describe(exception).Summary, failure.Summary, StringComparison.Ordinal);

    private FuzzRunResult<TState> Execute(ulong seed, long endTick, bool recordFingerprints, IReadOnlyList<int>? selection)
    {
        var state = settings.StateFactory();
        var engine = CreateEngine(seed, selection);

        var probe = new FuzzTickProbe();
        engine.AddMiddleware(probe);

        var sink = new InMemoryStateFingerprintSink();
        if (recordFingerprints)
            engine.AddMiddleware(new StateFingerprintMiddleware<TState>(state, settings.Serializer, sink, intervalTicks: 1));

        var invariants = CreateInvariants();
        if (invariants.Count > 0)
            engine.AddMiddleware(new InvariantMiddleware<TState>(state, invariants, checkEveryTicks: 1));

        Exception? error = null;

        try
        {
            engine.RunTicks(state, seed, SimTime.Zero, new SimTime(endTick));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            error = ex;
        }

        return new FuzzRunResult<TState>(state, error, probe.LastTick, sink.Records);
    }

    private SimulationEngine<TState> CreateEngine(ulong seed, IReadOnlyList<int>? selection)
    {
        var engine = new SimulationEngine<TState>();
        settings.ConfigureEngine(engine);

        if (BuildPlan(seed) is { IsEmpty: false } plan)
        {
            var inbox = new ScheduledCommandInbox();
            plan.ScheduleInto(inbox, selection?.ToHashSet());
            engine.AttachScheduledInbox(inbox);
        }

        return engine;
    }

    private FuzzCommandPlan? BuildPlan(ulong seed)
    {
        if (settings.Commands is not { } configureCommands)
            return null;

        var plan = new FuzzCommandPlan(seed, settings.EndTick);
        configureCommands(plan);

        if (settings.OnlyCommands is { Count: > 0 } selection && selection[^1] >= plan.Count)
            throw new InvalidOperationException(
                $"OnlyCommands refers to command #{selection[^1]}, but the plan for seed {seed} has {plan.Count} commands.");

        return plan;
    }

    private IReadOnlyList<IInvariant<TState>> CreateInvariants()
        => settings.Invariants is { } factory ? factory().ToArray() : [];

    private FuzzFailure? ToFailure(ulong seed, FuzzRunResult<TState> result, IReadOnlyList<int>? selection)
    {
        if (result.Error is not { } error)
            return null;

        var (kind, summary) = Describe(error);
        var identity = FuzzFailureIdentity.Of(error);
        var tick = error is InvariantViolationException violation ? violation.Time.Tick : result.LastTick;

        return new FuzzFailure(
            seed,
            kind,
            tick,
            summary,
            ComputeFailureDiff(seed, tick, result.State, selection),
            ArtifactsDir: null,
            ReproCode(seed, tick, kind, selection))
        {
            ReproTicks = ReproTicks(tick),
            Identity = identity.Name,
            Signature = FailureSignature.Of(identity)
        };
    }

    private FuzzFailure? ShrinkIfNeeded(ulong seed, FuzzRunResult<TState> result)
    {
        var failure = ToFailure(seed, result, settings.OnlyCommands);

        if (failure is null || !settings.Shrink || result.Error is not { } error)
            return failure;

        if (BuildPlan(seed) is not { IsEmpty: false } plan)
            return failure;

        var identity = FuzzFailureIdentity.Of(error);
        var original = settings.OnlyCommands ?? Enumerable.Range(0, plan.Count).ToArray();

        var shrink = CommandShrinker.Minimize(
            original,
            candidate => Execute(seed, settings.EndTick, recordFingerprints: false, candidate) is { Error: { } replayed }
                         && FuzzFailureIdentity.Of(replayed) == identity,
            settings.ShrinkBudget);

        var minimal = ToFailure(
            seed,
            Execute(seed, settings.EndTick, recordFingerprints: false, shrink.Indices),
            shrink.Indices);

        if (minimal is null || minimal.Kind != failure.Kind)
            return failure with { ShrinkSkipReason = "the failure did not reproduce while shrinking; the simulation may be non-deterministic, try CheckDeterminism" };

        return minimal with
        {
            OriginalCommandCount = original.Count,
            MinimalCommandIndices = shrink.Indices,
            MinimalCommands = plan.Describe(shrink.Indices),
            ShrinkAttempts = shrink.Attempts,
            ShrinkBudgetExhausted = shrink.BudgetExhausted
        };
    }

    private static (FuzzFailureKind Kind, string Summary) Describe(Exception error)
        => error is InvariantViolationException violation
            ? (FuzzFailureKind.InvariantViolation, $"{violation.InvariantName}: {violation.Detail}")
            : (FuzzFailureKind.Exception, $"{error.GetType().Name}: {error.Message}");

    private FuzzFailure BuildNonDeterminismFailure(ulong seed, long tick)
    {
        var left = Execute(seed, tick, recordFingerprints: false, settings.OnlyCommands);
        var right = Execute(seed, tick, recordFingerprints: false, settings.OnlyCommands);

        return new FuzzFailure(
            seed,
            FuzzFailureKind.NonDeterminism,
            tick,
            $"Two runs of the same seed diverged at tick {tick}.",
            SafeDiff(() => settings.Serializer.Serialize(left.State), () => settings.Serializer.Serialize(right.State)),
            ArtifactsDir: null,
            ReproCode(seed, tick, FuzzFailureKind.NonDeterminism, settings.OnlyCommands))
        {
            ReproTicks = ReproTicks(tick),
            Identity = FuzzFailureIdentity.NonDeterminism.Name,
            Signature = FailureSignature.Of(FuzzFailureIdentity.NonDeterminism),
            ShrinkSkipReason = ShrinkSkipReasonFor(seed)
        };
    }

    private string? ShrinkSkipReasonFor(ulong seed)
        => settings.Shrink && BuildPlan(seed) is { IsEmpty: false }
            ? "non-deterministic failures are not shrunk: replays of the same seed disagree, so a smaller command set cannot be checked reliably"
            : null;

    private IReadOnlyList<StateChange> ComputeFailureDiff(ulong seed, long failureTick, TState failedState, IReadOnlyList<int>? selection)
        => SafeDiff(
            () => failureTick > 0
                ? settings.Serializer.Serialize(Execute(seed, failureTick - 1, recordFingerprints: false, selection).State)
                : settings.Serializer.Serialize(settings.StateFactory()),
            () => settings.Serializer.Serialize(failedState));

    private static IReadOnlyList<StateChange> SafeDiff(Func<string> before, Func<string> after)
    {
        try
        {
            return StateDiff.Compute(before(), after());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return [];
        }
    }

    private static long? FindDivergenceTick(FuzzRunResult<TState> first, FuzzRunResult<TState> second)
    {
        var left = first.Fingerprints;
        var right = second.Fingerprints;
        var shared = Math.Min(left.Count, right.Count);

        for (var i = 0; i < shared; i++)
        {
            if (left[i].Tick != right[i].Tick)
                return Math.Min(left[i].Tick, right[i].Tick);

            if (!string.Equals(left[i].Hash, right[i].Hash, StringComparison.Ordinal))
                return left[i].Tick;
        }

        if (left.Count != right.Count)
            return (left.Count > right.Count ? left[shared] : right[shared]).Tick;

        if (!string.Equals(first.ErrorSignature, second.ErrorSignature, StringComparison.Ordinal))
            return Math.Min(FailureTick(first), FailureTick(second));

        return null;
    }

    private static long FailureTick(FuzzRunResult<TState> result)
        => result.Error switch
        {
            InvariantViolationException violation => violation.Time.Tick,
            null => long.MaxValue,
            _ => result.LastTick
        };

    private long ReproTicks(long failureTick)
        => settings.Commands is null ? failureTick : settings.EndTick;

    private string ReproCode(ulong seed, long tick, FuzzFailureKind kind, IReadOnlyList<int>? selection)
    {
        var code = $".Seeds({seed}, {seed}).Ticks({ReproTicks(tick)})";

        if (selection is not null)
            code += $".OnlyCommands([{string.Join(", ", selection)}])";

        return kind == FuzzFailureKind.NonDeterminism ? $"{code}.CheckDeterminism(true)" : code;
    }
}
