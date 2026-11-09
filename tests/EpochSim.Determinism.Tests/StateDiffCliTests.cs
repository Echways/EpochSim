using EpochSim;
using EpochSim.Cli.App;
using EpochSim.Cli.Domain;
using EpochSim.Execution;
using EpochSim.Samples.Population;

[Collection("Console")]
public sealed class StateDiffCliTests : IDisposable
{
    private const string RunId = "run-diff";
    private const long PokeTick = 7;

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"epochsim-statediff-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task Bisect_OnViolatedInvariant_PrintsStateDiffAndWritesFile()
    {
        RecordPokedRun(snapshotEveryTick: false);

        var (rc, output) = await RunCli("bisect", _root, RunId, "--end-tick", "20");

        Assert.Equal(2, rc);
        Assert.Contains($"FirstViolationTick={PokeTick}", output, StringComparison.Ordinal);
        Assert.Contains($"StateDiff (tick {PokeTick - 1} -> {PokeTick}):", output, StringComparison.Ordinal);
        Assert.Contains("$.Population: 100 -> -400", output, StringComparison.Ordinal);
        Assert.Contains("EventsAtTick=1", output, StringComparison.Ordinal);
        Assert.True(output.IndexOf("Message=", StringComparison.Ordinal) < output.IndexOf("StateDiff", StringComparison.Ordinal));

        var diffFile = Path.Combine(_root, RunId, "minrepro", $"tick-{PokeTick}", "statediff.txt");
        Assert.True(File.Exists(diffFile));
        Assert.Contains("$.Population: 100 -> -400", File.ReadAllText(diffFile), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Bisect_WithoutViolation_PrintsNoStateDiff()
    {
        RecordRun(snapshotEveryTick: false, poke: false);

        var (rc, output) = await RunCli("bisect", _root, RunId, "--end-tick", "20");

        Assert.Equal(0, rc);
        Assert.DoesNotContain("StateDiff", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VerifyRun_MismatchWithoutSnapshot_ReportsUnavailable()
    {
        RecordRun(snapshotEveryTick: false, poke: false);
        TamperFingerprint(tick: 3);

        var (rc, output) = await RunCli("verify-run", _root, RunId);

        Assert.Equal(3, rc);
        Assert.Contains("VerifyMismatchTick=3", output, StringComparison.Ordinal);
        Assert.Contains("StateDiffUnavailable: no snapshot at tick 3", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VerifyRun_MismatchWithSnapshot_PrintsSnapshotToReplayDiff()
    {
        RecordRun(snapshotEveryTick: true, poke: false);
        var snapshotPath = Path.Combine(_root, RunId, "snapshots", "snapshot-1.json");
        File.WriteAllText(snapshotPath, File.ReadAllText(snapshotPath).Replace("\"Population\":100", "\"Population\":999", StringComparison.Ordinal));

        var (rc, output) = await RunCli("verify-run", _root, RunId);

        Assert.Equal(3, rc);
        Assert.Contains("VerifyMismatchTick=1", output, StringComparison.Ordinal);
        Assert.Contains("StateDiff (snapshot -> replay, tick 1):", output, StringComparison.Ordinal);
        Assert.Contains("$.Population: 999 -> 100", output, StringComparison.Ordinal);
    }

    private void RecordPokedRun(bool snapshotEveryTick)
        => RecordRun(snapshotEveryTick, poke: true);

    private void RecordRun(bool snapshotEveryTick, bool poke)
    {
        var adapter = new PopulationDomainAdapter();
        var state = adapter.CreateInitialState();
        var engine = new SimulationEngine<WorldState>();
        adapter.ConfigureEngine(engine);

        if (poke)
        {
            engine.AddSystem(
                "Poke",
                tick: ctx =>
                {
                    if (ctx.Time.Tick == PokeTick - 1)
                        ctx.Scheduler.ScheduleNextTick(new PopulationDeltaEvent(-500));
                });
        }

        var builder = EpochSimRun.For(state)
            .WithRootDirectory(_root)
            .WithRunId(RunId)
            .WithDomain(adapter.Name)
            .WithEventLog(adapter.Codec)
            .WithStateFingerprints(adapter.Serializer, everyTicks: 1);

        if (snapshotEveryTick)
            builder.WithSnapshots(adapter.Serializer, everyTicks: 1);

        using var run = builder.Build();
        run.RunTicks(engine, seed: 1, endTickInclusive: 12);
    }

    private void TamperFingerprint(long tick)
    {
        var path = Path.Combine(_root, RunId, "statefp.jsonl");
        var lines = File.ReadAllLines(path)
            .Select(line => line.StartsWith($"{{\"t\":{tick},", StringComparison.Ordinal)
                ? $"{{\"t\":{tick},\"h\":\"{new string('0', 64)}\"}}"
                : line);
        File.WriteAllLines(path, lines);
    }

    private static async Task<(int ExitCode, string Output)> RunCli(params string[] args)
    {
        var original = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);

        try
        {
            var rc = await new CliApp().RunAsync(args);
            return (rc, writer.ToString());
        }
        finally
        {
            Console.SetOut(original);
        }
    }
}
