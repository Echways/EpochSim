using EpochSim;
using EpochSim.Fuzzing;
using EpochSim.Samples.Fuzzing;
using EpochSim.Testing;

[Collection("GoldenEnvironment")]
public sealed class SimAssertTests : IDisposable
{
    private const string UpdateVariable = "EPOCHSIM_UPDATE_GOLDEN";

    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"epochsim-golden-{Guid.NewGuid():N}");
    private readonly string? _originalUpdateValue = Environment.GetEnvironmentVariable(UpdateVariable);

    public SimAssertTests()
        => Environment.SetEnvironmentVariable(UpdateVariable, null);

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(UpdateVariable, _originalUpdateValue);

        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void IsDeterministic_OnDeterministicSystem_Passes()
    {
        SimAssert.IsDeterministic<JobQueueState>(f => f
            .State(() => new JobQueueState())
            .Engine(e => e.AddSystem(new JobQueueSystem()))
            .Seeds(1, 20)
            .Ticks(100));
    }

    [Fact]
    public void IsDeterministic_OnNonDeterministicSystem_FailsWithSeedInMessage()
    {
        var exception = Assert.Throws<SimulationAssertionException>(() =>
            SimAssert.IsDeterministic<StaticCounterState>(f => f
                .State(() => new StaticCounterState())
                .Engine(e => e.AddSystem(new StaticCounterSystem()))
                .Seeds(1, 3)
                .Ticks(10)));

        Assert.Contains("seed=1", exception.Message, StringComparison.Ordinal);
        Assert.Contains("NonDeterminism", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void IsDeterministic_ForcesCheckEvenWhenConfiguredOff()
    {
        Assert.Throws<SimulationAssertionException>(() =>
            SimAssert.IsDeterministic<StaticCounterState>(f => f
                .State(() => new StaticCounterState())
                .Engine(e => e.AddSystem(new StaticCounterSystem()))
                .CheckDeterminism(false)
                .Seeds(1, 1)
                .Ticks(10)));
    }

    [Fact]
    public void InvariantsHold_OnSystemWithBug_FailsWithSeedAndTick()
    {
        var exception = Assert.Throws<SimulationAssertionException>(() =>
            SimAssert.InvariantsHold<JobQueueState>(f => f
                .State(() => new JobQueueState())
                .Engine(e => e.AddSystem(new JobQueueSystem()))
                .Invariants(() => [new QueueNonNegativeInvariant()])
                .Seeds(1, 10)
                .Ticks(500)));

        Assert.Contains("seed=4", exception.Message, StringComparison.Ordinal);
        Assert.Contains("tick=426", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InvariantsHold_OnHealthySystem_Passes()
    {
        SimAssert.InvariantsHold<JobQueueState>(f => f
            .State(() => new JobQueueState())
            .Engine(e => e.AddSystem(new JobQueueSystem()))
            .Invariants(() => [new QueueNonNegativeInvariant()])
            .Seeds(1, 3)
            .Ticks(500));
    }

    [Fact]
    public void InvariantsHold_DoesNotForceDeterminismCheck()
    {
        SimAssert.InvariantsHold<StaticCounterState>(f => f
            .State(() => new StaticCounterState())
            .Engine(e => e.AddSystem(new StaticCounterSystem()))
            .Seeds(1, 3)
            .Ticks(10));
    }

    [Fact]
    public void NoFailures_OnPassedReport_DoesNotThrow()
    {
        var report = Epoch.Fuzz<JobQueueState>(f => f
            .State(() => new JobQueueState())
            .Engine(_ => { })
            .Seeds(1, 2)
            .Ticks(5));

        SimAssert.NoFailures(report);
    }

    [Fact]
    public void NoFailures_OnFailedReport_ThrowsWithReportText()
    {
        var report = Epoch.Fuzz<JobQueueState>(f => f
            .State(() => new JobQueueState())
            .Engine(e => e.AddSystem("Boom", tick: _ => throw new InvalidOperationException("boom")))
            .Seeds(1, 2)
            .Ticks(5));

        var exception = Assert.Throws<SimulationAssertionException>(() => SimAssert.NoFailures(report));

        Assert.Equal(report.ToText(), exception.Message);
    }

    [Fact]
    public void Assertions_RejectNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => SimAssert.NoFailures(null!));
        Assert.Throws<ArgumentNullException>(() => SimAssert.IsDeterministic<JobQueueState>(null!));
        Assert.Throws<ArgumentNullException>(() => SimAssert.InvariantsHold<JobQueueState>(null!));
    }

    [Fact]
    public void TestingAssembly_DoesNotReferenceTestFrameworks()
    {
        var references = typeof(SimAssert).Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty);

        Assert.DoesNotContain(references, name =>
            name.StartsWith("xunit", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("nunit", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("Microsoft.VisualStudio.TestTools", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("MSTest", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MatchesGolden_CreatesFileOnFirstRunAndPassesOnSecond()
    {
        var path = GoldenPath();

        SimAssert.MatchesGolden<JobQueueState>(JobQueue, path, seed: 1);
        var created = File.ReadAllText(path);
        SimAssert.MatchesGolden<JobQueueState>(JobQueue, path, seed: 1);

        Assert.Equal(created, File.ReadAllText(path));
        Assert.Contains("\"100\"", created, StringComparison.Ordinal);
        Assert.Contains("\"200\"", created, StringComparison.Ordinal);
        Assert.Contains("\"250\"", created, StringComparison.Ordinal);
        Assert.DoesNotContain("\"150\"", created, StringComparison.Ordinal);
    }

    [Fact]
    public void MatchesGolden_OnChangedBehavior_ReportsFirstDivergingCheckpoint()
    {
        var path = GoldenPath();
        SimAssert.MatchesGolden<JobQueueState>(JobQueue, path, seed: 1);

        var exception = Assert.Throws<SimulationAssertionException>(() =>
            SimAssert.MatchesGolden<JobQueueState>(
                f => ConfigureJobQueue(f).Engine(e =>
                {
                    e.AddSystem(new JobQueueSystem());
                    e.AddSystem("Extra", tick: ctx => ctx.State.Completed++);
                }),
                path,
                seed: 1));

        Assert.Contains("tick 100", exception.Message, StringComparison.Ordinal);
        Assert.Contains(UpdateVariable, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MatchesGolden_WithUpdateVariable_OverwritesFile()
    {
        var path = GoldenPath();
        SimAssert.MatchesGolden<JobQueueState>(JobQueue, path, seed: 1);
        var original = File.ReadAllText(path);

        Environment.SetEnvironmentVariable(UpdateVariable, "1");
        SimAssert.MatchesGolden<JobQueueState>(
            f => ConfigureJobQueue(f).Engine(e => e.AddSystem("Extra", tick: ctx => ctx.State.Completed++)),
            path,
            seed: 1);

        Assert.NotEqual(original, File.ReadAllText(path));
    }

    [Fact]
    public void MatchesGolden_WithDifferentSeed_Fails()
    {
        var path = GoldenPath();
        SimAssert.MatchesGolden<JobQueueState>(JobQueue, path, seed: 1);

        var exception = Assert.Throws<SimulationAssertionException>(() =>
            SimAssert.MatchesGolden<JobQueueState>(JobQueue, path, seed: 2));

        Assert.Contains("seed 1", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MatchesGolden_WithCorruptedFile_Fails()
    {
        var path = GoldenPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ not json");

        Assert.Throws<SimulationAssertionException>(() =>
            SimAssert.MatchesGolden<JobQueueState>(JobQueue, path, seed: 1));
    }

    [Fact]
    public void MatchesGolden_WhenRunFails_Throws()
    {
        var exception = Assert.Throws<SimulationAssertionException>(() =>
            SimAssert.MatchesGolden<JobQueueState>(
                f => f.State(() => new JobQueueState())
                    .Engine(e => e.AddSystem("Boom", tick: _ => throw new InvalidOperationException("boom")))
                    .Ticks(10),
                GoldenPath(),
                seed: 1));

        Assert.Contains("boom", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MatchesGolden_WithoutTicks_Throws()
    {
        Assert.Throws<SimulationAssertionException>(() =>
            SimAssert.MatchesGolden<JobQueueState>(f => ConfigureJobQueue(f).Ticks(0), GoldenPath(), seed: 1));
    }

    private string GoldenPath()
        => Path.Combine(_directory, "nested", "golden.json");

    private static void JobQueue(FuzzBuilder<JobQueueState> f)
        => ConfigureJobQueue(f);

    private static FuzzBuilder<JobQueueState> ConfigureJobQueue(FuzzBuilder<JobQueueState> f)
        => f.State(() => new JobQueueState())
            .Engine(e => e.AddSystem(new JobQueueSystem()))
            .Ticks(250);
}
