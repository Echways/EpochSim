using System.Text.Json;
using EpochSim;
using EpochSim.Execution;
using EpochSim.Fuzzing;
using EpochSim.Kernel.Time;
using EpochSim.Kernel.Validation;
using EpochSim.Samples.Fuzzing;

public sealed class FuzzFailureStoreTests : IDisposable
{
    private const string SuiteName = "accounts";
    private const long RunTicks = 100;

    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"epochsim-store-{Guid.NewGuid():N}");
    private readonly string _path;

    public FuzzFailureStoreTests()
    {
        Directory.CreateDirectory(_directory);
        _path = Path.Combine(_directory, "epochsim-failures.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void Store_KeepsOneEntryPerSignatureAndListsOtherSeeds()
    {
        var report = RunAccounts(buggy: true, seeds: (1, 5));

        Assert.Equal(5, report.Failures.Count);
        var entry = Assert.Single(new FuzzFailureStore(_path).Load());
        Assert.Equal(SuiteName, entry.Name);
        Assert.Equal("EpochSim.Samples.Fuzzing.AccountState", entry.StateType);
        Assert.Equal(1UL, entry.Seed);
        Assert.Equal([2UL, 3UL, 4UL, 5UL], entry.OtherSeeds);
        Assert.Equal(FuzzFailureKind.InvariantViolation, entry.Kind);
        Assert.Equal("BalanceNonNegative", entry.Identity);
        Assert.Equal(report.Failures[0].Tick, entry.Tick);
        Assert.Equal(RunTicks, entry.ReproTicks);
        Assert.Equal(report.Failures[0].MinimalCommandIndices, entry.CommandIndices);
        Assert.Equal(report.Failures[0].Signature, entry.Signature);
        Assert.False(entry.Fixed);
        Assert.InRange(DateTimeOffset.UtcNow - entry.FirstSeen, TimeSpan.Zero, TimeSpan.FromMinutes(5));
        Assert.Equal(TimeSpan.Zero, entry.FirstSeen.Offset);
    }

    [Fact]
    public void Signature_IsStableAcrossSeedsAndDiffersAcrossBugs()
    {
        var invariantBug = RunAccounts(buggy: true, seeds: (1, 3));
        var exceptionBug = Epoch.Fuzz<AccountState>(f => f
            .State(() => new AccountState())
            .Engine(e => e.AddSystem("Boom", tick: _ => throw new InvalidOperationException("boom")))
            .Seeds(1, 2)
            .Ticks(3));

        Assert.Single(invariantBug.Failures.Select(failure => failure.Signature).Distinct());
        Assert.Single(exceptionBug.Failures.Select(failure => failure.Signature).Distinct());
        Assert.NotEqual(invariantBug.Failures[0].Signature, exceptionBug.Failures[0].Signature);
        Assert.Equal(16, invariantBug.Failures[0].Signature.Length);
    }

    [Fact]
    public void SecondRun_ReportsFailuresAsKnown()
    {
        RunAccounts(buggy: true, seeds: (1, 3));
        var firstSeen = Assert.Single(new FuzzFailureStore(_path).Load()).FirstSeen;

        var second = RunAccounts(buggy: true, seeds: (1, 4));

        Assert.Empty(second.NewFailures);
        Assert.Equal(4, second.KnownFailures.Count);
        Assert.All(second.Failures, failure => Assert.True(failure.IsKnown));
        Assert.Contains("Known failures", second.ToText(), StringComparison.Ordinal);
        Assert.DoesNotContain("New failures", second.ToText(), StringComparison.Ordinal);

        var entry = Assert.Single(new FuzzFailureStore(_path).Load());
        Assert.Equal(firstSeen, entry.FirstSeen);
        Assert.Equal([2UL, 3UL, 4UL], entry.OtherSeeds);
    }

    [Fact]
    public void DifferentBug_IsNewNextToTheStoredOne()
    {
        RunAccounts(buggy: true, seeds: (1, 2), invariantName: "BalanceNonNegative");

        var second = RunAccounts(buggy: true, seeds: (1, 2), invariantName: "BalanceNeverBelowZero");

        Assert.Equal(2, second.NewFailures.Count);
        Assert.Empty(second.KnownFailures);
        Assert.Contains("New failures", second.ToText(), StringComparison.Ordinal);
        Assert.Equal(2, new FuzzFailureStore(_path).Load().Count);
    }

    [Fact]
    public void StoredSeeds_RunFirstRegardlessOfConfiguredRange()
    {
        RunAccounts(buggy: true, seeds: (7, 7));
        var order = new List<ulong>();

        var report = RunAccounts(buggy: true, seeds: (1, 10), configure: f => f
            .Parallelism(1)
            .Shrink(false)
            .Commands(plan =>
            {
                order.Add(plan.Seed);
                AccountLedger.PlanCommands(plan);
            }));

        Assert.Equal(7UL, order.First());
        Assert.Equal(10, report.SeedsRun);
        Assert.Equal(Enumerable.Range(1, 10).Select(seed => (ulong)seed).Order(), order.Distinct().Order());
    }

    [Fact]
    public void StoredSeeds_RunEvenWhenRangeExcludesThem()
    {
        RunAccounts(buggy: true, seeds: (3, 3));

        var report = RunAccounts(buggy: true, seeds: (100, 101));

        Assert.Equal(3, report.SeedsRun);
        Assert.Contains(report.Failures, failure => failure.Seed == 3);
    }

    [Fact]
    public void FixedBug_IsMarkedFixedAndReportedOnce()
    {
        RunAccounts(buggy: true, seeds: (1, 3));

        var afterFix = RunAccounts(buggy: false, seeds: (50, 50));

        Assert.True(afterFix.Passed);
        Assert.Equal([1UL], afterFix.FixedSeeds);
        Assert.Contains("Previously failing seed 1 now passes.", afterFix.ToText(), StringComparison.Ordinal);
        Assert.True(Assert.Single(new FuzzFailureStore(_path).Load()).Fixed);

        var again = RunAccounts(buggy: false, seeds: (50, 50));

        Assert.Empty(again.FixedSeeds);
        Assert.DoesNotContain("Previously failing", again.ToText(), StringComparison.Ordinal);
        Assert.Single(new FuzzFailureStore(_path).Load());
    }

    [Fact]
    public void FixedBug_ThatReturns_IsReportedAsNewAndReopened()
    {
        RunAccounts(buggy: true, seeds: (1, 2));
        RunAccounts(buggy: false, seeds: (1, 2));

        var regression = RunAccounts(buggy: true, seeds: (1, 2));

        Assert.Equal(2, regression.NewFailures.Count);
        Assert.False(Assert.Single(new FuzzFailureStore(_path).Load()).Fixed);
    }

    [Fact]
    public void Store_IsNotCreatedUnlessConfigured()
    {
        Epoch.Fuzz<AccountState>(f => f
            .State(() => new AccountState())
            .Engine(e => AccountLedger.Register(e))
            .Invariants(() => [new BalanceNonNegativeInvariant()])
            .Commands(AccountLedger.PlanCommands)
            .Seeds(1, 2)
            .Ticks(RunTicks));

        Assert.False(File.Exists(_path));
        Assert.Empty(Directory.GetFileSystemEntries(_directory));
    }

    [Fact]
    public void Store_FailuresOfOtherSuitesAreLeftAlone()
    {
        RunAccounts(buggy: true, seeds: (1, 2));

        Epoch.Fuzz<AccountState>(f => f
            .Name("other-suite")
            .State(() => new AccountState())
            .Engine(e => e.AddSystem("Boom", tick: _ => throw new InvalidOperationException("boom")))
            .Seeds(9, 9)
            .Ticks(3)
            .FailureStore(_path));

        var entries = new FuzzFailureStore(_path).Load();
        Assert.Equal([SuiteName, "other-suite"], entries.Select(entry => entry.Name).Order());
        Assert.Equal(1UL, entries.Single(entry => entry.Name == SuiteName).Seed);
    }

    [Fact]
    public void Name_DefaultsToTheFullStateTypeName()
    {
        Epoch.Fuzz<AccountState>(f => f
            .State(() => new AccountState())
            .Engine(e => e.AddSystem("Boom", tick: _ => throw new InvalidOperationException("boom")))
            .Seeds(1, 1)
            .Ticks(3)
            .FailureStore(_path));

        Assert.Equal(typeof(AccountState).FullName, Assert.Single(new FuzzFailureStore(_path).Load()).Name);
    }

    [Fact]
    public void Store_WithoutCommandsKeepsNullCommandIndices()
    {
        Epoch.Fuzz<AccountState>(f => f
            .State(() => new AccountState())
            .Engine(e => e.AddSystem("Boom", tick: _ => throw new InvalidOperationException("boom")))
            .Seeds(1, 1)
            .Ticks(3)
            .FailureStore(_path));

        var entry = Assert.Single(new FuzzFailureStore(_path).Load());
        Assert.Null(entry.CommandIndices);
        Assert.Equal(0, entry.Tick);
        Assert.Equal(entry.Tick, entry.ReproTicks);
        Assert.Equal("System.InvalidOperationException", entry.Identity);
    }

    [Fact]
    public async Task Store_ConcurrentUpdates_KeepTheFileValid()
    {
        var store = new FuzzFailureStore(_path);

        await Task.WhenAll(Enumerable.Range(0, 24).Select(index => Task.Run(() =>
            store.Update(entries => [.. entries, StoredFailure($"suite-{index:D2}", (ulong)index)]))));

        var entries = store.Load();
        Assert.Equal(24, entries.Count);
        Assert.Equal(24, entries.Select(entry => entry.Name).Distinct().Count());
        using var document = JsonDocument.Parse(File.ReadAllText(_path));
        Assert.Equal(24, document.RootElement.GetProperty("failures").GetArrayLength());
        Assert.Equal([Path.GetFileName(_path)], Directory.GetFiles(_directory).Select(Path.GetFileName));
    }

    [Fact]
    public async Task Store_ConcurrentFuzzRuns_KeepEveryRunsFailures()
    {
        await Task.WhenAll(Enumerable.Range(0, 6).Select(index => Task.Run(() =>
            Epoch.Fuzz<AccountState>(f => f
                .Name($"parallel-{index}")
                .State(() => new AccountState())
                .Engine(e => e.AddSystem("Boom", tick: _ => throw new InvalidOperationException("boom")))
                .Seeds(1, 3)
                .Ticks(3)
                .FailureStore(_path)))));

        var entries = new FuzzFailureStore(_path).Load();
        Assert.Equal(6, entries.Count);
        Assert.All(entries, entry => Assert.Equal([2UL, 3UL], entry.OtherSeeds));
    }

    [Fact]
    public void Store_WritesVersionedStableFile()
    {
        RunAccounts(buggy: true, seeds: (1, 2));
        var first = File.ReadAllText(_path);

        RunAccounts(buggy: true, seeds: (1, 2));

        Assert.Equal(first, File.ReadAllText(_path));
        using var document = JsonDocument.Parse(first);
        Assert.Equal(1, document.RootElement.GetProperty("version").GetInt32());
        var entry = document.RootElement.GetProperty("failures")[0];
        Assert.Equal(SuiteName, entry.GetProperty("name").GetString());
        Assert.Equal("InvariantViolation", entry.GetProperty("kind").GetString());
        Assert.Equal(1UL, entry.GetProperty("seed").GetUInt64());
        Assert.False(entry.GetProperty("fixed").GetBoolean());
        Assert.EndsWith("\n", first, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_MissingFile_ReturnsNoEntries()
        => Assert.Empty(new FuzzFailureStore(_path).Load());

    [Fact]
    public void Load_FileWithOnlyRequiredFields_FillsDefaults()
    {
        File.WriteAllText(_path, """
            { "version": 1, "failures": [
              { "name": "legacy", "seed": 5, "kind": "Exception", "tick": 3, "signature": "abc" }
            ] }
            """);

        var entry = Assert.Single(new FuzzFailureStore(_path).Load());
        Assert.Equal("legacy", entry.Name);
        Assert.Equal(5UL, entry.Seed);
        Assert.Equal(FuzzFailureKind.Exception, entry.Kind);
        Assert.Empty(entry.OtherSeeds);
        Assert.Null(entry.CommandIndices);
        Assert.False(entry.Fixed);
    }

    [Fact]
    public void Load_FileFromNewerSchema_IsRejectedWithTheVersion()
    {
        File.WriteAllText(_path, """{ "version": 2, "failures": [] }""");

        var error = Assert.Throws<InvalidDataException>(() => new FuzzFailureStore(_path).Load());

        Assert.Contains("version 2", error.Message, StringComparison.Ordinal);
        Assert.Contains(_path, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_CorruptFile_IsRejectedAndNeverOverwritten()
    {
        File.WriteAllText(_path, "{ not json");

        Assert.Throws<InvalidDataException>(() => new FuzzFailureStore(_path).Load());
        Assert.Throws<InvalidDataException>(() => RunAccounts(buggy: true, seeds: (1, 2)));
        Assert.Equal("{ not json", File.ReadAllText(_path));
    }

    private FuzzReport RunAccounts(
        bool buggy,
        (ulong From, ulong To) seeds,
        string invariantName = "BalanceNonNegative",
        Action<FuzzBuilder<AccountState>>? configure = null)
        => Epoch.Fuzz<AccountState>(f =>
        {
            f.Name(SuiteName)
                .State(() => new AccountState())
                .Engine(engine => ConfigureAccounts(engine, buggy))
                .Invariants(() => [new NamedBalanceInvariant(invariantName)])
                .Commands(AccountLedger.PlanCommands)
                .Seeds(seeds.From, seeds.To)
                .Ticks(RunTicks)
                .FailureStore(_path);

            configure?.Invoke(f);
        });

    private static void ConfigureAccounts(SimulationEngine<AccountState> engine, bool buggy)
    {
        engine.OnCommand<DepositCommand>((state, command, _) => state.Balance += command.Amount);
        engine.OnCommand<WithdrawCommand>((state, command, _) =>
            state.Balance = buggy ? state.Balance - command.Amount : Math.Max(0, state.Balance - command.Amount));
    }

    private static FuzzStoredFailure StoredFailure(string name, ulong seed)
        => new(
            name,
            "Some.State",
            seed,
            FuzzFailureKind.Exception,
            "System.InvalidOperationException",
            3,
            3,
            $"signature-{seed}",
            null,
            [],
            DateTimeOffset.UnixEpoch,
            false);

    private sealed class NamedBalanceInvariant(string name) : IInvariant<AccountState>
    {
        public string Name => name;

        public bool Check(SimTime time, AccountState state, out string message)
        {
            message = state.Balance >= 0 ? string.Empty : $"Balance={state.Balance}";
            return state.Balance >= 0;
        }
    }
}
