using EpochSim.Cli.App;

[Collection("Console")]
public sealed class FuzzCliTests : IDisposable
{
    private readonly string _out = Path.Combine(Path.GetTempPath(), $"epochsim-fuzzcli-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_out))
            Directory.Delete(_out, recursive: true);
    }

    [Fact]
    public async Task Fuzz_OnBuggyAdapter_Returns2AndPrintsSeedDiffAndRepro()
    {
        var (rc, output) = await RunCli("fuzz", "--adapter", "fuzzbug", "--seeds", "1..10", "--ticks", "500", "--out", _out);

        Assert.Equal(2, rc);
        Assert.Contains("SeedsRun=10 Failures=1", output, StringComparison.Ordinal);
        Assert.Contains("seed=4 tick=426", output, StringComparison.Ordinal);
        Assert.Contains("$.Pending", output, StringComparison.Ordinal);
        Assert.Contains($"epochsim fuzz --adapter fuzzbug --seeds 4 --ticks 426 --out {_out}", output, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(_out, "fuzz-4", "manifest.json")));
    }

    [Fact]
    public async Task Fuzz_ReproCommand_FailsOnSameSeedAndTick()
    {
        var (rc, output) = await RunCli("fuzz", "--adapter", "fuzzbug", "--seeds", "4", "--ticks", "426", "--out", _out);

        Assert.Equal(2, rc);
        Assert.Contains("seed=4 tick=426", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fuzz_OnPopulationAdapter_Returns0()
    {
        var (rc, output) = await RunCli("fuzz", "--seeds", "1..20", "--ticks", "100", "--parallel", "2", "--out", _out);

        Assert.Equal(0, rc);
        Assert.Contains("SeedsRun=20 Failures=0", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fuzz_CheckDeterminism_OnDeterministicAdapter_Returns0()
    {
        var (rc, _) = await RunCli("fuzz", "--seeds", "1..5", "--ticks", "50", "--check-determinism", "--out", _out);

        Assert.Equal(0, rc);
    }

    [Theory]
    [InlineData("--seeds", "nope")]
    [InlineData("--seeds", "9..1")]
    [InlineData("--ticks", "-1")]
    [InlineData("--parallel", "0")]
    [InlineData("--adapter", "unknown-adapter")]
    [InlineData("--bogus", "1")]
    [InlineData("--ticks", "")]
    public async Task Fuzz_InvalidArguments_Return2WithoutRunning(string option, string value)
    {
        var (rc, output) = await RunCli("fuzz", option, value, "--out", _out);

        Assert.Equal(2, rc);
        Assert.DoesNotContain("SeedsRun=", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fuzz_MissingOptionValue_Returns2()
    {
        var (rc, _) = await RunCli("fuzz", "--ticks");

        Assert.Equal(2, rc);
    }

    [Fact]
    public async Task Fuzz_Help_PrintsUsageAndReturns0()
    {
        var (rc, output) = await RunCli("fuzz", "--help");

        Assert.Equal(0, rc);
        Assert.Contains("--check-determinism", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fuzz_NoShrinkOption_IsAcceptedAndDocumented()
    {
        var (rc, output) = await RunCli("fuzz", "--adapter", "fuzzbug", "--seeds", "4", "--ticks", "426", "--no-shrink", "--out", _out);
        var (_, help) = await RunCli("fuzz", "--help");

        Assert.Equal(2, rc);
        Assert.Contains("seed=4 tick=426", output, StringComparison.Ordinal);
        Assert.Contains("--no-shrink", help, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListAdapters_IncludesFuzzBug()
    {
        var (rc, output) = await RunCli("list-adapters");

        Assert.Equal(0, rc);
        Assert.Contains("fuzzbug", output, StringComparison.Ordinal);
    }

    private static async Task<(int ExitCode, string Output)> RunCli(params string[] args)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        Console.SetError(TextWriter.Null);

        try
        {
            var rc = await new CliApp().RunAsync(args);
            return (rc, writer.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }
}
