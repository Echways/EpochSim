using EpochSim;
using EpochSim.Fuzzing;
using EpochSim.Kernel.Messaging;
using EpochSim.Samples.Onboarding;

public sealed class FuzzOnboardingTests
{
    [Fact]
    public void Step_WithInventoryThatKnowsNothingAboutEpochSim_FindsTheRareBug()
    {
        var report = Epoch.Fuzz<Inventory>(f => f
            .State(() => new Inventory())
            .Step((inventory, ctx) => inventory.Tick(ctx.Rng.AsRandom()))
            .Invariant("StockNonNegative", inventory => inventory.Stock >= 0)
            .Seeds(1, 1000)
            .Ticks(500));

        Assert.False(report.Passed);
        Assert.InRange(report.Failures.Count, 1, 200);
        Assert.All(report.Failures, failure =>
        {
            Assert.Equal(FuzzFailureKind.InvariantViolation, failure.Kind);
            Assert.StartsWith("StockNonNegative: ", failure.Summary, StringComparison.Ordinal);
            Assert.Contains(failure.StateDiff, change => change.Path == "$.Stock");
        });
    }

    [Fact]
    public void Step_FailureIsReproducibleFromItsReportedSeedAndTick()
    {
        var report = FuzzInventory(f => f.Seeds(1, 1000).Ticks(500));
        var failure = report.Failures[0];

        var reproduced = FuzzInventory(f => f.Seeds(failure.Seed, failure.Seed).Ticks(failure.Tick));
        var earlier = FuzzInventory(f => f.Seeds(failure.Seed, failure.Seed).Ticks(failure.Tick - 1));

        Assert.Equal(failure.Tick, Assert.Single(reproduced.Failures).Tick);
        Assert.True(earlier.Passed);
    }

    [Fact]
    public void Step_ReportDoesNotDependOnParallelism()
    {
        var sequential = FuzzInventory(f => f.Seeds(1, 300).Ticks(500).Parallelism(1));
        var parallel = FuzzInventory(f => f.Seeds(1, 300).Ticks(500).Parallelism(8));

        Assert.Equal(
            sequential.Failures.Select(failure => (failure.Seed, failure.Tick)),
            parallel.Failures.Select(failure => (failure.Seed, failure.Tick)));
    }

    [Fact]
    public void Step_SeesEveryTickInOrderAndADeterministicRng()
    {
        var observed = new List<(long Tick, int Draw)>();

        Epoch.Fuzz<Inventory>(f => f
            .State(() => new Inventory())
            .Step((_, ctx) => observed.Add((ctx.Tick, ctx.Rng.NextInt(0, 1000))))
            .Seeds(5, 5)
            .Ticks(9)
            .Parallelism(1));

        var again = new List<(long Tick, int Draw)>();
        Epoch.Fuzz<Inventory>(f => f
            .State(() => new Inventory())
            .Step((_, ctx) => again.Add((ctx.Tick, ctx.Rng.NextInt(0, 1000))))
            .Seeds(5, 5)
            .Ticks(9)
            .Parallelism(1));

        Assert.Equal(Enumerable.Range(0, 10).Select(tick => (long)tick), observed.Select(entry => entry.Tick));
        Assert.Equal(observed, again);
    }

    [Fact]
    public void Step_TimeProviderAdvancesOneTickDurationPerTick()
    {
        var times = new List<DateTimeOffset>();

        Epoch.Fuzz<Inventory>(f => f
            .State(() => new Inventory())
            .Step((_, ctx) => times.Add(ctx.TimeProvider.GetUtcNow()))
            .Seeds(1, 1)
            .Ticks(3)
            .Parallelism(1));

        Assert.Equal(
            Enumerable.Range(0, 4).Select(tick => StepContext.ClockStart + tick * StepContext.TickDuration),
            times);
    }

    [Fact]
    public void Step_TimeProviderKeepsTimeDependentLogicDeterministic()
    {
        var report = Epoch.Fuzz<TimedState>(f => f
            .State(() => new TimedState())
            .Step((state, ctx) => state.LastSeenUnixSeconds = ctx.TimeProvider.GetUtcNow().ToUnixTimeSeconds())
            .CheckDeterminism()
            .Seeds(1, 5)
            .Ticks(20));

        Assert.True(report.Passed);
    }

    [Fact]
    public void Step_SeveralStepsRunInRegistrationOrderOnEveryTick()
    {
        var calls = new List<string>();

        Epoch.Fuzz<Inventory>(f => f
            .State(() => new Inventory())
            .Step((_, ctx) => calls.Add($"a{ctx.Tick}"))
            .Step((_, ctx) => calls.Add($"b{ctx.Tick}"))
            .Seeds(1, 1)
            .Ticks(1)
            .Parallelism(1));

        Assert.Equal(["a0", "b0", "a1", "b1"], calls);
    }

    [Fact]
    public void Step_CombinesWithEngineConfiguration()
    {
        var order = new List<string>();

        Epoch.Fuzz<Inventory>(f => f
            .State(() => new Inventory())
            .Engine(e => e.AddSystem("Engine", tick: _ => order.Add("engine")))
            .Step((_, _) => order.Add("step"))
            .Seeds(1, 1)
            .Ticks(0)
            .Parallelism(1));

        Assert.Equal(["engine", "step"], order);
    }

    [Fact]
    public void Step_ExceptionIsReportedAsExceptionFailure()
    {
        var report = Epoch.Fuzz<Inventory>(f => f
            .State(() => new Inventory())
            .Step((_, ctx) =>
            {
                if (ctx.Tick == 3)
                    throw new InvalidOperationException("kaput");
            })
            .Seeds(1, 1)
            .Ticks(10));

        var failure = Assert.Single(report.Failures);
        Assert.Equal(FuzzFailureKind.Exception, failure.Kind);
        Assert.Equal(3, failure.Tick);
    }

    [Fact]
    public void Invariant_NameAppearsInTheReport()
    {
        var report = Epoch.Fuzz<Inventory>(f => f
            .State(() => new Inventory())
            .Step((_, _) => { })
            .Invariant("StockAtLeastOne", inventory => inventory.Stock >= 100)
            .Seeds(1, 1)
            .Ticks(5));

        var failure = Assert.Single(report.Failures);
        Assert.Equal(0, failure.Tick);
        Assert.Equal("StockAtLeastOne", failure.Identity);
        Assert.Contains("StockAtLeastOne", report.ToText(), StringComparison.Ordinal);
    }

    [Fact]
    public void Invariant_CombinesWithInvariantFactoryAndOtherPredicates()
    {
        var report = Epoch.Fuzz<Inventory>(f => f
            .State(() => new Inventory())
            .Step((_, _) => { })
            .Invariants(() => [new FailingInventoryInvariant()])
            .Invariant("First", _ => true)
            .Invariant("Second", _ => false)
            .Seeds(1, 1)
            .Ticks(0));

        var failure = Assert.Single(report.Failures);
        Assert.Equal("FactoryInvariant", failure.Identity);
    }

    [Fact]
    public void Invariant_PredicatesAreCheckedInOrder()
    {
        var report = Epoch.Fuzz<Inventory>(f => f
            .State(() => new Inventory())
            .Step((_, _) => { })
            .Invariant("First", _ => false)
            .Invariant("Second", _ => false)
            .Seeds(1, 1)
            .Ticks(0));

        Assert.Equal("First", Assert.Single(report.Failures).Identity);
    }

    [Fact]
    public void Invariant_InvalidArguments_Throw()
    {
        Assert.Throws<ArgumentException>(() => Epoch.Fuzz<Inventory>(f => f.Invariant(" ", _ => true)));
        Assert.Throws<ArgumentNullException>(() => Epoch.Fuzz<Inventory>(f => f.Invariant("Name", null!)));
        Assert.Throws<ArgumentNullException>(() => Epoch.Fuzz<Inventory>(f => f.Step(null!)));
        Assert.Throws<ArgumentNullException>(() => Epoch.Fuzz<Inventory>(f => f.Project(null!)));
    }

    [Fact]
    public void Fuzz_WithoutEngineAndStep_ExplainsWhatIsMissing()
    {
        var error = Assert.Throws<InvalidOperationException>(() => Epoch.Fuzz<Inventory>(f => f
            .State(() => new Inventory()).Seeds(1, 2).Ticks(3)));

        Assert.Contains("Engine", error.Message, StringComparison.Ordinal);
        Assert.Contains("Step", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Project_LetsUnserializableStateBeFingerprintedAndDiffed()
    {
        var report = Epoch.Fuzz<GraphState>(f => f
            .State(() => new GraphState())
            .Step((graph, ctx) => graph.Weight += ctx.Rng.NextInt(1, 4))
            .Project(graph => new { graph.Weight })
            .Invariant("WeightBelowTen", graph => graph.Weight < 10)
            .CheckDeterminism()
            .Seeds(1, 3)
            .Ticks(50));

        Assert.Equal(3, report.Failures.Count);
        Assert.All(report.Failures, failure =>
        {
            Assert.Equal(FuzzFailureKind.InvariantViolation, failure.Kind);
            Assert.Contains(failure.StateDiff, change => change.Path == "$.Weight");
        });
    }

    [Fact]
    public void Project_WithoutProjection_CyclicStateFailsInTheDefaultSerializer()
    {
        var report = Epoch.Fuzz<GraphState>(f => f
            .State(() => new GraphState())
            .Step((graph, ctx) => graph.Weight += ctx.Rng.NextInt(1, 4))
            .CheckDeterminism()
            .Seeds(1, 1)
            .Ticks(5));

        var failure = Assert.Single(report.Failures);
        Assert.Equal(FuzzFailureKind.Exception, failure.Kind);
        Assert.Contains("cycle", failure.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Project_CannotBeCombinedWithSerializer()
    {
        var error = Assert.Throws<InvalidOperationException>(() => Epoch.Fuzz<GraphState>(f => f
            .State(() => new GraphState())
            .Step((_, _) => { })
            .Project(graph => new { graph.Weight })
            .Serializer(new EpochSim.Serialization.State.JsonStateSerializer<GraphState>())
            .Seeds(1, 1)
            .Ticks(1)));

        Assert.Contains("Project", error.Message, StringComparison.Ordinal);
        Assert.Contains("Serializer", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Project_WorksTogetherWithShrinking()
    {
        var report = Epoch.Fuzz<GraphState>(f => f
            .State(() => new GraphState())
            .Engine(e => e.OnCommand<AddWeight>((graph, command, _) => graph.Weight += command.Amount))
            .Project(graph => new { graph.Weight })
            .Invariant("WeightBelowTen", graph => graph.Weight < 10)
            .Commands(plan =>
            {
                for (var i = 0; i < 20; i++)
                    plan.Enqueue(i, new AddWeight(i == 7 ? 12 : 0));
            })
            .Seeds(1, 1)
            .Ticks(30));

        var failure = Assert.Single(report.Failures);
        Assert.Equal([7], failure.MinimalCommandIndices);
    }

    private static FuzzReport FuzzInventory(Action<FuzzBuilder<Inventory>> configure)
        => Epoch.Fuzz<Inventory>(f =>
        {
            f.State(() => new Inventory())
                .Step((inventory, ctx) => inventory.Tick(ctx.Rng.AsRandom()))
                .Invariant("StockNonNegative", inventory => inventory.Stock >= 0);

            configure(f);
        });

    private sealed class TimedState
    {
        public long LastSeenUnixSeconds { get; set; }
    }

    private sealed class GraphState
    {
        public GraphState()
            => Self = this;

        public GraphState Self { get; }
        public int Weight { get; set; }
    }

    private sealed record AddWeight(int Amount) : ICommand;

    private sealed class FailingInventoryInvariant : EpochSim.Kernel.Validation.IInvariant<Inventory>
    {
        public string Name => "FactoryInvariant";

        public bool Check(EpochSim.Kernel.Time.SimTime time, Inventory state, out string message)
        {
            message = "always";
            return false;
        }
    }
}
