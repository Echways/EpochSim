using EpochSim;
using EpochSim.Fuzzing;
using EpochSim.Samples.Fuzzing;
using EpochSim.Samples.Population;
using EpochSim.Serialization.EventLog;

public sealed class FuzzTests : IDisposable
{
    private const ulong BugSeed = 4;
    private const long BugTick = 426;
    private const long RunTicks = 500;

    private readonly string _artifactsRoot = Path.Combine(Path.GetTempPath(), $"epochsim-fuzz-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_artifactsRoot))
            Directory.Delete(_artifactsRoot, recursive: true);
    }

    [Fact]
    public void Fuzz_FindsSeedWithRareBug()
    {
        var report = FuzzJobQueue(f => f.Seeds(1, 10).Ticks(RunTicks));

        var failure = Assert.Single(report.Failures);
        Assert.False(report.Passed);
        Assert.Equal(10, report.SeedsRun);
        Assert.Equal(BugSeed, failure.Seed);
        Assert.Equal(FuzzFailureKind.InvariantViolation, failure.Kind);
        Assert.Equal(BugTick, failure.Tick);
        Assert.Contains("QueueNonNegative", failure.Summary, StringComparison.Ordinal);
        Assert.Contains(failure.StateDiff, change => change.Path == "$.Pending" && change.After == "-1");
        Assert.Equal(".Seeds(4, 4).Ticks(426)", failure.ReproCode);
    }

    [Fact]
    public void Fuzz_FailedSeed_ReproducesWithSingleSeedAtSameTick()
    {
        var reproduced = FuzzJobQueue(f => f.Seeds(BugSeed, BugSeed).Ticks(BugTick));
        var earlier = FuzzJobQueue(f => f.Seeds(BugSeed, BugSeed).Ticks(BugTick - 1));

        Assert.Equal(BugTick, Assert.Single(reproduced.Failures).Tick);
        Assert.True(earlier.Passed);
    }

    [Fact]
    public void Fuzz_Failures_DoNotDependOnParallelism()
    {
        var sequential = FuzzJobQueue(f => f.Seeds(1, 400).Ticks(RunTicks).Parallelism(1));
        var parallel = FuzzJobQueue(f => f.Seeds(1, 400).Ticks(RunTicks).Parallelism(8));

        Assert.NotEmpty(sequential.Failures);
        Assert.Equal(sequential.SeedsRun, parallel.SeedsRun);
        Assert.Equal(Project(sequential), Project(parallel));
        Assert.Equal(sequential.Failures.Select(f => f.Seed).Order(), sequential.Failures.Select(f => f.Seed));
    }

    [Fact]
    public void Fuzz_StopAtFirstFailure_WithSingleWorker_StopsAfterFirstFailingSeed()
    {
        var report = FuzzJobQueue(f => f.Seeds(1, 1000).Ticks(RunTicks).Parallelism(1).StopAtFirstFailure());

        Assert.Equal(BugSeed, Assert.Single(report.Failures).Seed);
        Assert.Equal((int)BugSeed, report.SeedsRun);
    }

    [Fact]
    public void Fuzz_CheckDeterminism_CatchesStaticState()
    {
        var report = Epoch.Fuzz<StaticCounterState>(f => f
            .State(() => new StaticCounterState())
            .Engine(e => e.AddSystem(new StaticCounterSystem()))
            .Seeds(1, 5)
            .Ticks(20)
            .CheckDeterminism());

        Assert.Equal([1UL, 2UL, 3UL, 4UL, 5UL], report.Failures.Select(f => f.Seed));
        Assert.All(report.Failures, failure =>
        {
            Assert.Equal(FuzzFailureKind.NonDeterminism, failure.Kind);
            Assert.Equal(1, failure.Tick);
            Assert.Contains(failure.StateDiff, change => change.Path == "$.Value");
            Assert.EndsWith(".CheckDeterminism(true)", failure.ReproCode, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Fuzz_CheckDeterminism_CatchesWallClockUsage()
    {
        var report = Epoch.Fuzz<StaticCounterState>(f => f
            .State(() => new StaticCounterState())
            .Engine(e => e.AddSystem("Clock", tick: ctx => ctx.State.Value = DateTime.UtcNow.Ticks))
            .Seeds(1, 3)
            .Ticks(10)
            .CheckDeterminism());

        Assert.Equal(3, report.Failures.Count);
        Assert.All(report.Failures, failure => Assert.Equal(FuzzFailureKind.NonDeterminism, failure.Kind));
    }

    [Fact]
    public void Fuzz_CheckDeterminism_AcceptsDeterministicLogic()
    {
        var report = FuzzJobQueue(f => f.Seeds(1, 30).Ticks(200).CheckDeterminism(), withInvariants: false);

        Assert.True(report.Passed);
        Assert.Equal(30, report.SeedsRun);
    }

    [Fact]
    public void Fuzz_ExceptionFromSystem_IsClassifiedAsException()
    {
        var report = Epoch.Fuzz<JobQueueState>(f => f
            .State(() => new JobQueueState())
            .Engine(e => e.AddSystem("Boom", tick: ctx =>
            {
                if (ctx.Time.Tick == 5)
                    throw new InvalidOperationException("boom");
            }))
            .Seeds(1, 2)
            .Ticks(10));

        Assert.Equal(2, report.Failures.Count);
        Assert.All(report.Failures, failure =>
        {
            Assert.Equal(FuzzFailureKind.Exception, failure.Kind);
            Assert.Equal(5, failure.Tick);
            Assert.Equal("InvalidOperationException: boom", failure.Summary);
        });
    }

    [Fact]
    public void Fuzz_OperationCanceledFromSystem_IsNotAFailure()
    {
        var report = Epoch.Fuzz<JobQueueState>(f => f
            .State(() => new JobQueueState())
            .Engine(e => e.AddSystem("Cancel", tick: _ => throw new OperationCanceledException()))
            .Seeds(1, 2)
            .Ticks(3));

        Assert.True(report.Passed);
        Assert.Equal(2, report.SeedsRun);
    }

    [Fact]
    public void ThrowIfFailed_ThrowsFuzzFailedExceptionWithReportText()
    {
        var failed = FuzzJobQueue(f => f.Seeds(1, 10).Ticks(RunTicks));

        var exception = Assert.Throws<FuzzFailedException>(failed.ThrowIfFailed);

        Assert.Equal(failed.ToText(), exception.Message);
        Assert.Same(failed, exception.Report);
        Assert.Contains("seed=4", exception.Message, StringComparison.Ordinal);
        Assert.Contains("tick=426", exception.Message, StringComparison.Ordinal);
        Assert.Contains("$.Pending", exception.Message, StringComparison.Ordinal);
        Assert.Contains(".Seeds(4, 4).Ticks(426)", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ThrowIfFailed_OnPassedReport_DoesNotThrow()
    {
        var passed = FuzzJobQueue(f => f.Seeds(1, 3).Ticks(50));

        passed.ThrowIfFailed();

        Assert.True(passed.Passed);
        Assert.Contains("SeedsRun=3", passed.ToText(), StringComparison.Ordinal);
    }

    [Fact]
    public void Commands_SameSeedProducesSameSchedule()
    {
        var first = RecordSchedule(seed: 7);
        var second = RecordSchedule(seed: 7);
        var other = RecordSchedule(seed: 8);

        Assert.NotEmpty(first);
        Assert.Equal(first, second);
        Assert.NotEqual(first, other);
    }

    [Fact]
    public void Commands_AreDeliveredAtScheduledTick()
    {
        var states = new List<WorldState>();

        var report = Epoch.Fuzz<WorldState>(f => f
            .State(() =>
            {
                var created = new WorldState { Population = 0 };
                states.Add(created);
                return created;
            })
            .Engine(e => e.OnCommand<GrowPopulationCommand>((state, command, _) => state.Population += command.Delta))
            .Commands(plan =>
            {
                plan.Enqueue(3, new GrowPopulationCommand(5));
                plan.Enqueue(plan.EndTick, new GrowPopulationCommand(7));
            })
            .Seeds(1, 1)
            .Ticks(10));

        Assert.True(report.Passed);
        Assert.Equal(12, Assert.Single(states).Population);
    }

    [Fact]
    public void Commands_OutsideRun_AreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Epoch.Fuzz<WorldState>(f => f
            .State(() => new WorldState())
            .Engine(_ => { })
            .Commands(plan => plan.Enqueue(plan.EndTick + 1, new GrowPopulationCommand(1)))
            .Seeds(1, 1)
            .Ticks(10)));
    }

    [Fact]
    public void ArtifactsRoot_WritesArtifactsForFailedSeed()
    {
        var report = FuzzJobQueue(f => f
            .Seeds(BugSeed, BugSeed)
            .Ticks(RunTicks)
            .ArtifactsRoot(_artifactsRoot)
            .EventCodec(new JsonEventCodecBuilder().Build()));

        var failure = Assert.Single(report.Failures);
        Assert.Equal(Path.Combine(_artifactsRoot, "fuzz-4"), failure.ArtifactsDir);
        Assert.True(File.Exists(Path.Combine(failure.ArtifactsDir!, "manifest.json")));
        Assert.True(File.Exists(Path.Combine(failure.ArtifactsDir!, "statefp.jsonl")));
        Assert.True(File.Exists(Path.Combine(failure.ArtifactsDir!, "failure-report.json")));
        Assert.Contains(failure.ArtifactsDir!, failure.ToText(), StringComparison.Ordinal);
    }

    [Fact]
    public void ArtifactsRoot_WithoutCodec_StillWritesManifestAndFingerprints()
    {
        var report = FuzzJobQueue(f => f.Seeds(BugSeed, BugSeed).Ticks(RunTicks).ArtifactsRoot(_artifactsRoot));

        var directory = Assert.Single(report.Failures).ArtifactsDir;
        Assert.True(File.Exists(Path.Combine(directory!, "manifest.json")));
        Assert.True(File.Exists(Path.Combine(directory!, "statefp.jsonl")));
    }

    [Fact]
    public void ArtifactsRoot_LimitsArtifactsToTenFailures()
    {
        var report = Epoch.Fuzz<JobQueueState>(f => f
            .State(() => new JobQueueState())
            .Engine(e => e.AddSystem("Boom", tick: _ => throw new InvalidOperationException("boom")))
            .Seeds(1, 15)
            .Ticks(3)
            .ArtifactsRoot(_artifactsRoot));

        Assert.Equal(15, report.Failures.Count);
        Assert.Equal(10, report.Failures.Count(failure => failure.ArtifactsDir is not null));
        Assert.All(report.Failures.Take(10), failure => Assert.NotNull(failure.ArtifactsDir));
    }

    [Fact]
    public void Fuzz_ReportsProgressForEverySeed()
    {
        var progress = new RecordingProgress();

        var report = FuzzJobQueue(f => f.Seeds(1, 25).Ticks(20).Progress(progress));

        Assert.Equal(report.SeedsRun, progress.Reports.Count);
        Assert.Equal(25, progress.Reports.Max(p => p.SeedsRun));
    }

    [Fact]
    public void Fuzz_EmptySeedList_Passes()
    {
        var report = FuzzJobQueue(f => f.Seeds(Array.Empty<ulong>()).Ticks(10));

        Assert.True(report.Passed);
        Assert.Equal(0, report.SeedsRun);
    }

    [Fact]
    public void Fuzz_SeedRangeEndingAtUlongMaxValue_Terminates()
    {
        var report = FuzzJobQueue(f => f.Seeds(ulong.MaxValue - 1, ulong.MaxValue).Ticks(5));

        Assert.True(report.Passed);
        Assert.Equal(2, report.SeedsRun);
    }

    [Fact]
    public void Fuzz_MissingRequiredParameters_ThrowsWithParameterName()
    {
        var noState = Assert.Throws<InvalidOperationException>(() => Epoch.Fuzz<JobQueueState>(f => f
            .Engine(_ => { }).Seeds(1, 2).Ticks(3)));
        var noEngine = Assert.Throws<InvalidOperationException>(() => Epoch.Fuzz<JobQueueState>(f => f
            .State(() => new JobQueueState()).Seeds(1, 2).Ticks(3)));
        var noTicks = Assert.Throws<InvalidOperationException>(() => Epoch.Fuzz<JobQueueState>(f => f
            .State(() => new JobQueueState()).Engine(_ => { }).Seeds(1, 2)));
        var noSeeds = Assert.Throws<InvalidOperationException>(() => Epoch.Fuzz<JobQueueState>(f => f
            .State(() => new JobQueueState()).Engine(_ => { }).Ticks(3)));

        Assert.Contains("State", noState.Message, StringComparison.Ordinal);
        Assert.Contains("Engine", noEngine.Message, StringComparison.Ordinal);
        Assert.Contains("Ticks", noTicks.Message, StringComparison.Ordinal);
        Assert.Contains("Seeds", noSeeds.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Fuzz_InvalidValues_Throw()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Epoch.Fuzz<JobQueueState>(f => f.Seeds(5, 4)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Epoch.Fuzz<JobQueueState>(f => f.Ticks(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Epoch.Fuzz<JobQueueState>(f => f.Parallelism(0)));
    }

    private static FuzzReport FuzzJobQueue(Action<FuzzBuilder<JobQueueState>> configure, bool withInvariants = true)
        => Epoch.Fuzz<JobQueueState>(f =>
        {
            f.State(() => new JobQueueState())
                .Engine(e => e.AddSystem(new JobQueueSystem()));

            if (withInvariants)
                f.Invariants(() => [new QueueNonNegativeInvariant()]);

            configure(f);
        });

    private static List<string> RecordSchedule(ulong seed)
    {
        var schedule = new List<string>();

        Epoch.Fuzz<WorldState>(f => f
            .State(() => new WorldState())
            .Engine(e => e.OnCommand<GrowPopulationCommand>((_, _, _) => { }))
            .Commands(plan =>
            {
                schedule.Clear();
                for (var i = 0; i < 5; i++)
                {
                    var tick = plan.Rng.NextInt(0, 50);
                    var delta = plan.Rng.NextInt(1, 9);
                    plan.Enqueue(tick, new GrowPopulationCommand(delta));
                    schedule.Add($"{tick}:{delta}");
                }
            })
            .Seeds(seed, seed)
            .Ticks(50)
            .Parallelism(1));

        return schedule;
    }

    private static IEnumerable<string> Project(FuzzReport report)
        => report.Failures.Select(failure =>
            $"{failure.Seed}|{failure.Kind}|{failure.Tick}|{failure.Summary}|{failure.ReproCode}|" +
            string.Join(";", failure.StateDiff.Select(change => $"{change.Path}:{change.Before}>{change.After}")));

    private sealed class RecordingProgress : IProgress<FuzzProgress>
    {
        private readonly object _gate = new();

        public List<FuzzProgress> Reports { get; } = [];

        public void Report(FuzzProgress value)
        {
            lock (_gate)
                Reports.Add(value);
        }
    }
}
