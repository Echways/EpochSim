using EpochSim;
using EpochSim.Fuzzing;
using EpochSim.Testing;
using RegressionFixtures;

public sealed class SimAssertRegressionTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"epochsim-regression-{Guid.NewGuid():N}");
    private readonly string _storePath;

    public SimAssertRegressionTests()
    {
        Directory.CreateDirectory(_directory);
        _storePath = Path.Combine(_directory, "epochsim-failures.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void Regression_OnBuggyLogic_FailsWithTheStoredScenario()
    {
        var entry = StoreBuggyAccountFailure();

        var error = Assert.Throws<SimulationAssertionException>(() =>
            SimAssert.Regression<EpochSim.Samples.Fuzzing.AccountState>(
                BuggyAccountSuite.Configure,
                entry.Seed,
                entry.ReproTicks,
                entry.CommandIndices));

        Assert.Contains($"seed={entry.Seed}", error.Message, StringComparison.Ordinal);
        Assert.Contains("BalanceNonNegative", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Regression_OnFixedLogic_Passes()
    {
        var entry = StoreBuggyAccountFailure();

        SimAssert.Regression<EpochSim.Samples.Fuzzing.AccountState>(
            FixedAccountSuite.Configure,
            entry.Seed,
            entry.ReproTicks,
            entry.CommandIndices);
    }

    [Fact]
    public void Regression_RunsExactlyTheGivenSeedAndIgnoresConfiguredStore()
    {
        var entry = StoreBuggyAccountFailure();
        var before = File.ReadAllText(_storePath);

        Assert.Throws<SimulationAssertionException>(() =>
            SimAssert.Regression<EpochSim.Samples.Fuzzing.AccountState>(
                fuzz =>
                {
                    BuggyAccountSuite.Configure(fuzz);
                    fuzz.FailureStore(_storePath);
                },
                entry.Seed,
                entry.ReproTicks,
                entry.CommandIndices));

        Assert.Equal(before, File.ReadAllText(_storePath));
    }

    [Fact]
    public void Regression_WithEmptyCommandSelection_DoesNotFail()
    {
        var entry = StoreBuggyAccountFailure();

        SimAssert.Regression<EpochSim.Samples.Fuzzing.AccountState>(
            BuggyAccountSuite.Configure,
            entry.Seed,
            entry.ReproTicks,
            []);
    }

    [Fact]
    public void StoredFailuresStayFixed_WhileBugIsPresent_Fails()
    {
        var entry = StoreBuggyAccountFailure();

        var error = Assert.Throws<SimulationAssertionException>(() =>
            SimAssert.StoredFailuresStayFixed<EpochSim.Samples.Fuzzing.AccountState>(BuggyAccountSuite.Configure, _storePath));

        Assert.Contains($"seed={entry.Seed}", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StoredFailuresStayFixed_ReplaysEveryRecordedSeed()
    {
        var entry = StoreBuggyAccountFailure();
        Assert.NotEmpty(entry.OtherSeeds);

        var error = Assert.Throws<SimulationAssertionException>(() =>
            SimAssert.StoredFailuresStayFixed<EpochSim.Samples.Fuzzing.AccountState>(BuggyAccountSuite.Configure, _storePath));

        foreach (var seed in entry.OtherSeeds)
            Assert.Contains($"seed={seed}", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StoredFailuresStayFixed_AfterFix_Passes()
    {
        StoreBuggyAccountFailure();

        SimAssert.StoredFailuresStayFixed<EpochSim.Samples.Fuzzing.AccountState>(FixedAccountSuite.Configure, _storePath);
    }

    [Fact]
    public void StoredFailuresStayFixed_WithoutStoreFile_Passes()
        => SimAssert.StoredFailuresStayFixed<EpochSim.Samples.Fuzzing.AccountState>(BuggyAccountSuite.Configure, _storePath);

    [Fact]
    public void StoredFailuresStayFixed_IgnoresOtherSuites()
    {
        Epoch.Fuzz<EpochSim.Samples.Fuzzing.StaticCounterState>(fuzz =>
        {
            NonDeterministicSuite.Configure(fuzz);
            fuzz.FailureStore(_storePath);
        });

        SimAssert.StoredFailuresStayFixed<EpochSim.Samples.Fuzzing.AccountState>(BuggyAccountSuite.Configure, _storePath);
    }

    [Fact]
    public void StoredFailuresStayFixed_DoesNotModifyTheStore()
    {
        StoreBuggyAccountFailure();
        var before = File.ReadAllText(_storePath);

        SimAssert.StoredFailuresStayFixed<EpochSim.Samples.Fuzzing.AccountState>(FixedAccountSuite.Configure, _storePath);

        Assert.Equal(before, File.ReadAllText(_storePath));
    }

    private FuzzStoredFailure StoreBuggyAccountFailure()
    {
        Epoch.Fuzz<EpochSim.Samples.Fuzzing.AccountState>(fuzz =>
        {
            BuggyAccountSuite.Configure(fuzz);
            fuzz.FailureStore(_storePath);
        });

        return Assert.Single(new FuzzFailureStore(_storePath).Load());
    }
}
