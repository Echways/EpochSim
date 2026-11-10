using System.Reflection;
using System.Text.RegularExpressions;
using EpochSim;
using EpochSim.Cli.App;
using EpochSim.Fuzzing;
using EpochSim.Samples.Fuzzing;
using EpochSim.Testing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RegressionFixtures;

[Collection("Console")]
public sealed partial class FailuresCliTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"epochsim-failures-cli-{Guid.NewGuid():N}");
    private readonly string _storePath;
    private readonly string _outDirectory;

    public FailuresCliTests()
    {
        Directory.CreateDirectory(_directory);
        _storePath = Path.Combine(_directory, "epochsim-failures.json");
        _outDirectory = Path.Combine(_directory, "tests");
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public async Task List_PrintsEveryStoredFailure()
    {
        StoreAccountFailures();

        var (rc, output) = await RunCli("failures", "list", "--store", _storePath);

        Assert.Equal(0, rc);
        Assert.Contains("accounts", output, StringComparison.Ordinal);
        Assert.Contains("seed=1", output, StringComparison.Ordinal);
        Assert.Contains("InvariantViolation", output, StringComparison.Ordinal);
        Assert.Contains("BalanceNonNegative", output, StringComparison.Ordinal);
        Assert.Contains("other seeds: 2, 3", output, StringComparison.Ordinal);
        Assert.Contains("open", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task List_WithoutStoreFile_ReportsNothingStored()
    {
        var (rc, output) = await RunCli("failures", "list", "--store", _storePath);

        Assert.Equal(0, rc);
        Assert.Contains("No stored failures", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Prune_RemovesOnlyFixedEntries()
    {
        StoreAccountFailures();
        Epoch.Fuzz<StaticCounterState>(fuzz =>
        {
            NonDeterministicSuite.Configure(fuzz);
            fuzz.FailureStore(_storePath);
        });
        Epoch.Fuzz<AccountState>(fuzz =>
        {
            FixedAccountSuite.Configure(fuzz);
            fuzz.FailureStore(_storePath);
        });
        Assert.Equal(2, new FuzzFailureStore(_storePath).Load().Count);
        Assert.Single(new FuzzFailureStore(_storePath).Load(), entry => entry.Fixed);

        var (rc, output) = await RunCli("failures", "prune", "--store", _storePath);

        Assert.Equal(0, rc);
        Assert.Contains("Pruned 1 fixed failure", output, StringComparison.Ordinal);
        var remaining = Assert.Single(new FuzzFailureStore(_storePath).Load());
        Assert.Equal(NonDeterministicSuite.SuiteName, remaining.Name);
    }

    [Theory]
    [InlineData("xunit", "[Fact]", "using Xunit;")]
    [InlineData("nunit", "[Test]", "using NUnit.Framework;")]
    [InlineData("mstest", "[TestMethod]", "using Microsoft.VisualStudio.TestTools.UnitTesting;")]
    public async Task ExportTest_GeneratesCompilableTestsThatFailUntilTheBugIsFixed(string framework, string attribute, string usingLine)
    {
        var entry = StoreAccountFailures();

        var (rc, _) = await RunCli(
            "failures", "export-test",
            "--framework", framework,
            "--configure", "RegressionFixtures.BuggyAccountSuite.Configure",
            "--out", _outDirectory,
            "--store", _storePath);

        Assert.Equal(0, rc);
        var file = Assert.Single(Directory.GetFiles(_outDirectory));
        Assert.Equal("BuggyAccountSuiteRegressionTests.cs", Path.GetFileName(file));
        var source = File.ReadAllText(file);
        Assert.Contains(usingLine, source, StringComparison.Ordinal);
        Assert.Contains(attribute, source, StringComparison.Ordinal);
        Assert.Contains($"Seed1_BalanceNonNegative_AtTick{entry.Tick}", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Seed2_", source, StringComparison.Ordinal);
        Assert.DoesNotContain("//", source, StringComparison.Ordinal);
        Assert.DoesNotContain("/*", source, StringComparison.Ordinal);

        var assembly = Compile(source);
        var generated = assembly.GetTypes().Single(type => type.Name == "BuggyAccountSuiteRegressionTests");
        var tests = TestMethods(generated);
        Assert.Single(tests);
        Assert.All(tests, test => Assert.Throws<SimulationAssertionException>(() => Invoke(generated, test)));

        var fixedSource = source.Replace("global::RegressionFixtures.BuggyAccountSuite.Configure", "global::RegressionFixtures.FixedAccountSuite.Configure", StringComparison.Ordinal);
        var fixedGenerated = Compile(fixedSource).GetTypes().Single(type => type.Name == "BuggyAccountSuiteRegressionTests");
        Assert.All(TestMethods(fixedGenerated), test => Invoke(fixedGenerated, test));
    }

    [Fact]
    public async Task ExportTest_PassesRecordedCommandIndicesAndTicks()
    {
        var entry = StoreAccountFailures();

        await RunCli(
            "failures", "export-test",
            "--framework", "xunit",
            "--configure", "RegressionFixtures.BuggyAccountSuite.Configure",
            "--out", _outDirectory,
            "--store", _storePath);

        var source = File.ReadAllText(Path.Combine(_outDirectory, "BuggyAccountSuiteRegressionTests.cs"));
        var indices = string.Join(", ", entry.CommandIndices!);
        Assert.Contains(
            $"SimAssert.Regression<global::EpochSim.Samples.Fuzzing.AccountState>(global::RegressionFixtures.BuggyAccountSuite.Configure, 1, {entry.ReproTicks}, [{indices}])",
            source,
            StringComparison.Ordinal);
        Assert.Contains("namespace RegressionFixtures;", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExportTest_ForNonDeterminism_EnablesTheDeterminismCheck()
    {
        Epoch.Fuzz<StaticCounterState>(fuzz =>
        {
            NonDeterministicSuite.Configure(fuzz);
            fuzz.FailureStore(_storePath);
        });

        var (rc, _) = await RunCli(
            "failures", "export-test",
            "--framework", "xunit",
            "--configure", "RegressionFixtures.NonDeterministicSuite.Configure",
            "--out", _outDirectory,
            "--store", _storePath);

        Assert.Equal(0, rc);
        var source = File.ReadAllText(Path.Combine(_outDirectory, "NonDeterministicSuiteRegressionTests.cs"));
        var generated = Compile(source).GetTypes().Single(type => type.Name == "NonDeterministicSuiteRegressionTests");
        var tests = TestMethods(generated);
        Assert.Contains(tests, test => test.Name.StartsWith("Seed1_NonDeterminism_AtTick", StringComparison.Ordinal));
        Assert.All(tests, test => Assert.Throws<SimulationAssertionException>(() => Invoke(generated, test)));
    }

    [Fact]
    public async Task ExportTest_WithSeveralSuites_RequiresName()
    {
        StoreAccountFailures();
        Epoch.Fuzz<StaticCounterState>(fuzz =>
        {
            NonDeterministicSuite.Configure(fuzz);
            fuzz.FailureStore(_storePath);
        });
        string[] common =
        [
            "failures", "export-test", "--framework", "xunit",
            "--configure", "RegressionFixtures.BuggyAccountSuite.Configure",
            "--out", _outDirectory, "--store", _storePath
        ];

        var (withoutName, _) = await RunCli(common);
        var (withName, _) = await RunCli([.. common, "--name", "accounts"]);

        Assert.Equal(2, withoutName);
        Assert.Equal(0, withName);
        Assert.Contains("Seed1_BalanceNonNegative", File.ReadAllText(Directory.GetFiles(_outDirectory).Single()), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--framework", "junit")]
    [InlineData("--configure", "NoMethod")]
    public async Task ExportTest_InvalidArguments_Return2WithoutWriting(string option, string value)
    {
        StoreAccountFailures();
        var arguments = new Dictionary<string, string>
        {
            ["--framework"] = "xunit",
            ["--configure"] = "RegressionFixtures.BuggyAccountSuite.Configure",
            ["--out"] = _outDirectory,
            ["--store"] = _storePath,
            [option] = value
        };

        var (rc, _) = await RunCli(["failures", "export-test", .. arguments.SelectMany(pair => new[] { pair.Key, pair.Value })]);

        Assert.Equal(2, rc);
        Assert.False(Directory.Exists(_outDirectory));
    }

    [Fact]
    public async Task ExportTest_WithoutStoredFailures_Returns2()
    {
        var (rc, _) = await RunCli(
            "failures", "export-test", "--framework", "xunit",
            "--configure", "RegressionFixtures.BuggyAccountSuite.Configure",
            "--out", _outDirectory, "--store", _storePath);

        Assert.Equal(2, rc);
    }

    [Fact]
    public async Task UnknownSubcommand_Returns2()
    {
        var (rc, _) = await RunCli("failures", "bogus");

        Assert.Equal(2, rc);
    }

    [Fact]
    public async Task Help_ListsSubcommands()
    {
        var (rc, output) = await RunCli("failures", "--help");

        Assert.Equal(0, rc);
        Assert.Contains("list", output, StringComparison.Ordinal);
        Assert.Contains("prune", output, StringComparison.Ordinal);
        Assert.Contains("export-test", output, StringComparison.Ordinal);
    }

    private FuzzStoredFailure StoreAccountFailures()
    {
        Epoch.Fuzz<AccountState>(fuzz =>
        {
            BuggyAccountSuite.Configure(fuzz);
            fuzz.FailureStore(_storePath);
        });

        return new FuzzFailureStore(_storePath).Load().Single(entry => entry.Name == BuggyAccountSuite.SuiteName);
    }

    private static Assembly Compile(string source)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));

        var compilation = CSharpCompilation.Create(
            $"Generated{Guid.NewGuid():N}",
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        var problems = result.Diagnostics.Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning).ToArray();
        Assert.True(result.Success && problems.Length == 0, string.Join(Environment.NewLine, problems.Select(problem => problem.ToString())));

        return Assembly.Load(stream.ToArray());
    }

    private static List<MethodInfo> TestMethods(Type generated)
        => generated.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => SeedMethodName().IsMatch(method.Name))
            .ToList();

    private static void Invoke(Type generated, MethodInfo method)
    {
        try
        {
            method.Invoke(Activator.CreateInstance(generated), null);
        }
        catch (TargetInvocationException error) when (error.InnerException is { } inner)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(inner).Throw();
        }
    }

    [GeneratedRegex(@"^Seed\d+_\w+_AtTick\d+(_\d+)?$")]
    private static partial Regex SeedMethodName();

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
