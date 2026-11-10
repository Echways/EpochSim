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
}
