using EpochSim;
using EpochSim.Fuzzing;
using EpochSim.Kernel.Messaging;
using EpochSim.Kernel.Time;
using EpochSim.Kernel.Validation;
using EpochSim.Samples.Fuzzing;

public sealed class FuzzShrinkTests
{
    private const int CommandCount = 300;
    private const long RunTicks = 60;
    private static readonly int[] CulpritIndices = [17, 120, 254];

    [Fact]
    public void Shrink_ReducesThreeCulpritsOutOfThreeHundredCommands()
    {
        var failure = Assert.Single(FuzzMarks(f => f.Seeds(1, 1)).Failures);

        Assert.Equal(FuzzFailureKind.InvariantViolation, failure.Kind);
        Assert.Equal(CommandCount, failure.OriginalCommandCount);
        Assert.Equal(CulpritIndices, failure.MinimalCommandIndices);
        Assert.True(failure.ShrinkAttempts > 0);
        Assert.False(failure.ShrinkBudgetExhausted);
        Assert.Null(failure.ShrinkSkipReason);
        Assert.Equal(
            ["MarkCommand", "MarkCommand", "MarkCommand"],
            failure.MinimalCommands.Select(command => command.Type));
        Assert.Equal(CulpritIndices, failure.MinimalCommands.Select(command => command.Index));
        Assert.Equal(CulpritIndices.Select(index => (long)(index % 50)), failure.MinimalCommands.Select(command => command.Tick));
    }

    [Fact]
    public void Shrink_ReportsMinimalScenarioTickAndRepro()
    {
        var failure = Assert.Single(FuzzMarks(f => f.Seeds(1, 1)).Failures);

        Assert.Equal(20, failure.Tick);
        Assert.Equal(".Seeds(1, 1).Ticks(60).OnlyCommands([17, 120, 254])", failure.ReproCode);
        Assert.Equal(RunTicks, failure.ReproTicks);
        Assert.Contains(failure.StateDiff, change => change.Path == "$.Marks[2]" && change.After == "120");
    }

    [Fact]
    public void Shrink_ResultDoesNotDependOnParallelism()
    {
        var sequential = FuzzMarks(f => f.Seeds(1, 12).Parallelism(1));
        var parallel = FuzzMarks(f => f.Seeds(1, 12).Parallelism(8));

        Assert.Equal(12, sequential.Failures.Count);
        Assert.Equal(Project(sequential), Project(parallel));
    }

    [Fact]
    public void OnlyCommands_WithMinimalIndices_ReproducesSameKind()
    {
        var shrunk = Assert.Single(FuzzMarks(f => f.Seeds(1, 1)).Failures);

        var reproduced = Assert.Single(FuzzMarks(f => f
            .Seeds(1, 1)
            .Ticks(RunTicks)
            .OnlyCommands(shrunk.MinimalCommandIndices!)).Failures);

        Assert.Equal(shrunk.Kind, reproduced.Kind);
        Assert.Equal(shrunk.Tick, reproduced.Tick);
        Assert.Equal(shrunk.Summary, reproduced.Summary);
    }

    [Fact]
    public void OnlyCommands_WithoutCulprit_Passes()
    {
        var report = FuzzMarks(f => f.Seeds(1, 1).OnlyCommands([17, 120]));

        Assert.True(report.Passed);
    }

    [Fact]
    public void OnlyCommands_IndexOutsidePlan_Throws()
    {
        var error = Assert.Throws<InvalidOperationException>(() => FuzzMarks(f => f.Seeds(1, 1).OnlyCommands([CommandCount])));

        Assert.Contains("OnlyCommands", error.Message, StringComparison.Ordinal);
        Assert.Contains(CommandCount.ToString(), error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OnlyCommands_NegativeIndex_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FuzzMarks(f => f.OnlyCommands([-1])));
    }

    [Fact]
    public void Shrink_WhenBudgetIsExhausted_ReturnsValidFailingScenario()
    {
        var report = FuzzMarks(f => f.Seeds(1, 1).ShrinkBudget(maxAttempts: 12, timeLimit: TimeSpan.FromMinutes(5)));

        var failure = Assert.Single(report.Failures);
        Assert.True(failure.ShrinkBudgetExhausted);
        Assert.True(failure.ShrinkAttempts <= 12);
        Assert.InRange(failure.MinimalCommandIndices!.Count, CulpritIndices.Length, CommandCount - 1);
        Assert.Subset(failure.MinimalCommandIndices.ToHashSet(), CulpritIndices.ToHashSet());
        Assert.Contains("budget", failure.ToText(), StringComparison.OrdinalIgnoreCase);

        var reproduced = FuzzMarks(f => f.Seeds(1, 1).OnlyCommands(failure.MinimalCommandIndices));
        Assert.Equal(failure.Kind, Assert.Single(reproduced.Failures).Kind);
    }

    [Fact]
    public void Shrink_WhenTimeLimitIsZero_ReturnsOriginalScenario()
    {
        var report = FuzzMarks(f => f.Seeds(1, 1).ShrinkBudget(maxAttempts: 1000, timeLimit: TimeSpan.Zero));

        var failure = Assert.Single(report.Failures);
        Assert.True(failure.ShrinkBudgetExhausted);
        Assert.Equal(0, failure.ShrinkAttempts);
        Assert.Equal(CommandCount, failure.MinimalCommandIndices!.Count);
    }

    [Fact]
    public void Shrink_WithoutCommands_IsNotRunAndFieldsAreEmpty()
    {
        var report = Epoch.Fuzz<JobQueueState>(f => f
            .State(() => new JobQueueState())
            .Engine(e => e.AddSystem(new JobQueueSystem()))
            .Invariants(() => [new QueueNonNegativeInvariant()])
            .Seeds(4, 4)
            .Ticks(500));

        var failure = Assert.Single(report.Failures);
        Assert.Equal(0, failure.OriginalCommandCount);
        Assert.Null(failure.MinimalCommandIndices);
        Assert.Empty(failure.MinimalCommands);
        Assert.Equal(0, failure.ShrinkAttempts);
        Assert.Null(failure.ShrinkSkipReason);
        Assert.DoesNotContain("Commands:", failure.ToText(), StringComparison.Ordinal);
        Assert.Equal(".Seeds(4, 4).Ticks(426)", failure.ReproCode);
    }

    [Fact]
    public void Shrink_Disabled_KeepsFullCommandPlan()
    {
        var failure = Assert.Single(FuzzMarks(f => f.Seeds(1, 1).Shrink(false)).Failures);

        Assert.Null(failure.MinimalCommandIndices);
        Assert.Equal(0, failure.ShrinkAttempts);
        Assert.Equal(".Seeds(1, 1).Ticks(60)", failure.ReproCode);
    }

    [Fact]
    public void Shrink_NonDeterministicFailure_IsNotShrunkAndExplainsWhy()
    {
        var report = Epoch.Fuzz<MarkState>(f => f
            .State(() => new MarkState())
            .Engine(e =>
            {
                e.OnCommand<MarkCommand>((state, command, _) => state.Marks.Add(command.Id));
                e.AddSystem("Clock", tick: ctx => ctx.State.Stamp = DateTime.UtcNow.Ticks);
            })
            .Commands(PlanMarks)
            .Seeds(1, 1)
            .Ticks(RunTicks)
            .CheckDeterminism());

        var failure = Assert.Single(report.Failures);
        Assert.Equal(FuzzFailureKind.NonDeterminism, failure.Kind);
        Assert.Null(failure.MinimalCommandIndices);
        Assert.Equal(0, failure.ShrinkAttempts);
        Assert.NotNull(failure.ShrinkSkipReason);
        Assert.Contains(failure.ShrinkSkipReason, failure.ToText(), StringComparison.Ordinal);
    }

    [Fact]
    public void Shrink_ExceptionFailure_MatchesOnExceptionType()
    {
        var report = Epoch.Fuzz<MarkState>(f => f
            .State(() => new MarkState())
            .Engine(e =>
            {
                e.OnCommand<MarkCommand>((state, command, _) =>
                {
                    if (command.Id is 40 or 41)
                        throw new InvalidOperationException($"bad {command.Id}");

                    state.Marks.Add(command.Id);
                });
            })
            .Commands(PlanMarks)
            .Seeds(1, 1)
            .Ticks(RunTicks));

        var failure = Assert.Single(report.Failures);
        Assert.Equal(FuzzFailureKind.Exception, failure.Kind);
        Assert.Single(failure.MinimalCommandIndices!);
        Assert.Contains(failure.MinimalCommandIndices![0], new[] { 40, 41 });
    }

    [Fact]
    public void ToText_ShowsShrinkSummaryCommandsAndDiff()
    {
        var failure = Assert.Single(FuzzMarks(f => f.Seeds(1, 1)).Failures);

        var text = failure.ToText();

        Assert.Contains($"Commands: {CommandCount} -> 3 (shrunk in {failure.ShrinkAttempts} attempts)", text, StringComparison.Ordinal);
        Assert.Contains("#17 tick 17: MarkCommand", text, StringComparison.Ordinal);
        Assert.Contains("#254 tick 4: MarkCommand", text, StringComparison.Ordinal);
        Assert.True(text.IndexOf("Commands:", StringComparison.Ordinal) < text.IndexOf("StateDiff:", StringComparison.Ordinal));
        Assert.EndsWith(".OnlyCommands([17, 120, 254])", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Shrink_IsEnabledByDefaultOnlyWithCommands()
    {
        var withCommands = FuzzMarks(f => f.Seeds(1, 1));
        var shrinkTrueWithoutCommands = Epoch.Fuzz<JobQueueState>(f => f
            .State(() => new JobQueueState())
            .Engine(e => e.AddSystem(new JobQueueSystem()))
            .Invariants(() => [new QueueNonNegativeInvariant()])
            .Seeds(4, 4)
            .Ticks(500)
            .Shrink());

        Assert.NotNull(Assert.Single(withCommands.Failures).MinimalCommandIndices);
        Assert.Null(Assert.Single(shrinkTrueWithoutCommands.Failures).MinimalCommandIndices);
    }

    [Fact]
    public void Shrink_AccountSample_ReducesTheLedgerToAHandfulOfCommands()
    {
        var report = Epoch.Fuzz<AccountState>(f => f
            .State(() => new AccountState())
            .Engine(AccountLedger.Register)
            .Invariants(() => [new BalanceNonNegativeInvariant()])
            .Commands(AccountLedger.PlanCommands)
            .Seeds(1, 20)
            .Ticks(100));

        var failure = report.Failures[0];
        Assert.Equal(AccountLedger.CommandsPerSeed, failure.OriginalCommandCount);
        Assert.InRange(failure.MinimalCommandIndices!.Count, 1, 5);
        Assert.Contains("BalanceNonNegative", failure.Summary, StringComparison.Ordinal);
    }

    private static void PlanMarks(FuzzCommandPlan plan)
    {
        for (var id = 0; id < CommandCount; id++)
            plan.Enqueue(id % 50, new MarkCommand(id));
    }

    private static FuzzReport FuzzMarks(Action<FuzzBuilder<MarkState>> configure)
        => Epoch.Fuzz<MarkState>(f =>
        {
            f.State(() => new MarkState())
                .Engine(e => e.OnCommand<MarkCommand>((state, command, _) => state.Marks.Add(command.Id)))
                .Invariants(() => [new CulpritInvariant()])
                .Commands(PlanMarks)
                .Ticks(RunTicks);

            configure(f);
        });

    private static IEnumerable<string> Project(FuzzReport report)
        => report.Failures.Select(failure =>
            $"{failure.Seed}|{failure.Kind}|{failure.Tick}|{failure.Summary}|{failure.ReproCode}|{failure.ShrinkAttempts}|" +
            string.Join(",", failure.MinimalCommandIndices ?? []));

    public sealed class MarkState
    {
        public List<int> Marks { get; set; } = [];
        public long Stamp { get; set; }
    }

    public sealed record MarkCommand(int Id) : ICommand;

    private sealed class CulpritInvariant : IInvariant<MarkState>
    {
        public string Name => "NoCulpritTrio";

        public bool Check(SimTime time, MarkState state, out string message)
        {
            var culpritsPresent = CulpritIndices.All(state.Marks.Contains);
            message = culpritsPresent ? "all three culprits delivered" : string.Empty;
            return !culpritsPresent;
        }
    }
}
