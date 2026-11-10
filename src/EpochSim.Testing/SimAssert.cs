using EpochSim.Fuzzing;

namespace EpochSim.Testing;

public static class SimAssert
{
    public static void IsDeterministic<TState>(Action<FuzzBuilder<TState>> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        NoFailures(Epoch.Fuzz<TState>(builder =>
        {
            configure(builder);
            builder.CheckDeterminism(true);
        }));
    }

    public static void InvariantsHold<TState>(Action<FuzzBuilder<TState>> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        NoFailures(Epoch.Fuzz(configure));
    }

    public static void NoFailures(FuzzReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        if (!report.Passed)
            throw new SimulationAssertionException(report.ToText());
    }

    public static void Regression<TState>(
        Action<FuzzBuilder<TState>> configure,
        ulong seed,
        long ticks,
        IReadOnlyList<int>? commandIndices = null)
    {
        ArgumentNullException.ThrowIfNull(configure);

        NoFailures(Replay(configure, seed, ticks, commandIndices));
    }

    public static void StoredFailuresStayFixed<TState>(Action<FuzzBuilder<TState>> configure, string storePath)
    {
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentException.ThrowIfNullOrWhiteSpace(storePath);

        var probe = new FuzzBuilder<TState>();
        configure(probe);
        var suiteName = probe.Build().Name;

        var failures = new FuzzFailureStore(storePath).Load()
            .Where(entry => entry.Name == suiteName)
            .SelectMany(entry => ReplayEntry(configure, entry))
            .ToArray();

        if (failures.Length > 0)
            throw new SimulationAssertionException(string.Join(Environment.NewLine + Environment.NewLine, failures.Select(failure => failure.ToText())));
    }

    public static void MatchesGolden<TState>(Action<FuzzBuilder<TState>> configure, string goldenPath, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentException.ThrowIfNullOrWhiteSpace(goldenPath);

        var builder = new FuzzBuilder<TState>();
        configure(builder);
        builder.Seeds(seed, seed);

        var settings = builder.Build();
        var actual = GoldenFile.Capture(FuzzRunner.Trace(settings, seed), seed, settings.EndTick);
        GoldenFile.Verify(goldenPath, actual);
    }

    private static IEnumerable<FuzzFailure> ReplayEntry<TState>(Action<FuzzBuilder<TState>> configure, FuzzStoredFailure entry)
    {
        var primary = Replay(configure, entry.Seed, entry.ReproTicks, entry.CommandIndices);
        var others = entry.OtherSeeds.Select(seed => Replay(configure, seed, ticks: null, commandIndices: null));

        return others.Prepend(primary).SelectMany(report => report.Failures);
    }

    private static FuzzReport Replay<TState>(
        Action<FuzzBuilder<TState>> configure,
        ulong seed,
        long? ticks,
        IReadOnlyList<int>? commandIndices)
        => Epoch.Fuzz<TState>(builder =>
        {
            configure(builder);
            builder.Seeds(seed, seed).Shrink(false).WithoutFailureStore();

            if (ticks is { } endTick)
                builder.Ticks(endTick);

            if (commandIndices is not null)
                builder.OnlyCommands(commandIndices);
        });
}
