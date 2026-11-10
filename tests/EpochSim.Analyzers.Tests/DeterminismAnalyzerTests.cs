using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

public sealed partial class DeterminismAnalyzerTests
{
    private static readonly string[] WallClock =
    [
        "var v = {|ESIM001:DateTime.Now|};",
        "var v = {|ESIM001:DateTime.UtcNow|};",
        "var v = {|ESIM001:DateTime.Today|};",
        "var v = {|ESIM001:DateTimeOffset.Now|};",
        "var v = {|ESIM001:DateTimeOffset.UtcNow|};",
        "var v = {|ESIM001:Environment.TickCount|};",
        "var v = {|ESIM001:Environment.TickCount64|};",
        "var v = {|ESIM001:TimeProvider.System|};",
        "var v = {|ESIM001:new Stopwatch()|};",
        "var v = {|ESIM001:Stopwatch.StartNew()|};",
        "var v = {|ESIM001:Stopwatch.GetTimestamp()|};"
    ];

    private static readonly string[] Randomness =
    [
        "var v = {|ESIM002:Random.Shared|};",
        "var v = {|ESIM002:new Random()|};",
        "var v = {|ESIM002:new Random(1)|};",
        "var v = {|ESIM002:Guid.NewGuid()|};",
        "var v = {|ESIM002:RandomNumberGenerator.GetInt32(5)|};"
    ];

    private static readonly string[] Concurrency =
    [
        "{|ESIM003:Task.Run(() => { })|};",
        "{|ESIM003:Task.Factory.StartNew(() => { })|};",
        "var v = {|ESIM003:new Thread(() => { })|};",
        "{|ESIM003:ThreadPool.QueueUserWorkItem(_ => { })|};",
        "{|ESIM003:Parallel.For(0, 1, _ => { })|};",
        "var v = {|ESIM003:new[] { 1 }.AsParallel()|};"
    ];

    private static readonly string[] Hashing =
    [
        "var v = {|ESIM004:\"abc\".GetHashCode()|};",
        "object o = 1; var v = {|ESIM004:o.GetHashCode()|};",
        "var d = new ConcurrentDictionary<int, int>(); {|ESIM004:foreach (var p in d) { }|}",
        "var b = new ConcurrentBag<int>(); {|ESIM004:foreach (var p in b) { }|}",
        "var q = new ConcurrentQueue<int>(); var v = {|ESIM004:q.Select(x => x)|};"
    ];

    private static readonly string[] ExternalInput =
    [
        "var v = {|ESIM005:File.ReadAllText(\"a\")|};",
        "var v = {|ESIM005:File.Exists(\"a\")|};",
        "var v = {|ESIM005:Directory.GetFiles(\".\")|};",
        "var v = {|ESIM005:new HttpClient()|};",
        "var v = {|ESIM005:Environment.GetEnvironmentVariable(\"A\")|};",
        "var v = {|ESIM005:Environment.MachineName|};"
    ];

    private static readonly string[] Representative =
    [
        WallClock[1],
        Randomness[0],
        Concurrency[0],
        Hashing[0],
        ExternalInput[0]
    ];

    private static readonly Dictionary<string, Func<string, string>> Contexts = new()
    {
        ["AddSystem lambda"] = SimulationSource.Lambda,
        ["OnCommand lambda"] = SimulationSource.OnCommandLambda,
        ["ISystem"] = SimulationSource.SystemClass,
        ["IInvariant"] = SimulationSource.InvariantClass,
        ["ICommandHandler"] = SimulationSource.CommandHandlerClass,
        ["[Deterministic] method"] = SimulationSource.DeterministicMethod,
        ["[Deterministic] class"] = SimulationSource.DeterministicClass,
        ["method group"] = SimulationSource.MethodGroup
    };

    public static TheoryData<string, string> EveryConstructInCommonContexts()
    {
        var data = new TheoryData<string, string>();
        var statements = WallClock.Concat(Randomness).Concat(Concurrency).Concat(Hashing).Concat(ExternalInput);

        foreach (var statement in statements)
        {
            data.Add("AddSystem lambda", statement);
            data.Add("ISystem", statement);
        }

        return data;
    }

    public static TheoryData<string, string> RepresentativeConstructInEveryContext()
    {
        var data = new TheoryData<string, string>();

        foreach (var context in Contexts.Keys)
        {
            foreach (var statement in Representative)
                data.Add(context, statement);
        }

        return data;
    }

    public static TheoryData<string> EveryConstruct()
    {
        var data = new TheoryData<string>();

        foreach (var statement in WallClock.Concat(Randomness).Concat(Concurrency).Concat(Hashing).Concat(ExternalInput))
            data.Add(statement);

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryConstructInCommonContexts))]
    public Task ReportsConstruct(string context, string statement)
        => AnalyzerVerifier.Verify(Contexts[context](statement));

    [Theory]
    [MemberData(nameof(RepresentativeConstructInEveryContext))]
    public Task ReportsRepresentativeConstructInEverySimulationContext(string context, string statement)
        => AnalyzerVerifier.Verify(Contexts[context](statement));

    [Theory]
    [MemberData(nameof(EveryConstruct))]
    public Task IgnoresConstructsOutsideSimulationCode(string statement)
        => AnalyzerVerifier.Verify(SimulationSource.Outside(StripMarkup(statement)));

    [Fact]
    public Task ReportsAsyncLambdaAndAwaitInsideSystemLambda()
        => AnalyzerVerifier.Verify(
            SimulationSource.Lambda("").Replace(
                "tick: ctx => {  }",
                "tick: {|ESIM003:async|} ctx => { {|ESIM003:await Task.Yield()|}; }"));

    [Fact]
    public Task ReportsAwaitInsideSystemMethod()
        => AnalyzerVerifier.Verify(
            SimulationSource.SystemClass("").Replace(
                "public void Tick(TickContext<S> ctx) {  }",
                "public async void Tick(TickContext<S> ctx) { {|ESIM003:await Task.Yield()|}; }"));

    [Fact]
    public Task ReportsNestedLambdaInsideSystemLambda()
        => AnalyzerVerifier.Verify(SimulationSource.Lambda(
            "Action inner = () => { var v = {|ESIM001:DateTime.UtcNow|}; }; inner();"));

    [Fact]
    public Task ReportsLocalFunctionInsideSystemLambda()
        => AnalyzerVerifier.Verify(SimulationSource.Lambda(
            "void Local() { var v = {|ESIM001:DateTime.UtcNow|}; } Local();"));

    [Fact]
    public Task ReportsTypeNestedInDeterministicClass()
        => AnalyzerVerifier.Verify(SimulationSource.DeterministicClass(
            "var v = new Nested(); v.Run();").Replace(
                "public static void Run()",
                "public sealed class Nested { public void Run() { var t = {|ESIM001:DateTime.UtcNow|}; } }\n    public static void Run()"));

    [Fact]
    public Task ReportsEachConstructOnce()
        => AnalyzerVerifier.Verify(SimulationSource.Lambda(
            "var a = {|ESIM001:DateTime.UtcNow|}; var b = {|ESIM002:Random.Shared|}.Next(); var c = {|ESIM002:Guid.NewGuid()|};"));

    [Fact]
    public Task IgnoresDeterministicHashCodes()
        => AnalyzerVerifier.Verify(SimulationSource.Lambda(
            "var a = 5.GetHashCode(); var b = \"abc\".GetHashCode(StringComparison.Ordinal);"));

    [Fact]
    public Task IgnoresOrderedCollections()
        => AnalyzerVerifier.Verify(SimulationSource.Lambda(
            "var l = new List<int>(); foreach (var x in l) { } var a = new int[0]; foreach (var y in a) { } var s = new SortedDictionary<int, int>(); foreach (var z in s) { }"));

    [Fact]
    public Task IgnoresClassesWithoutSimulationInterfaces()
        => AnalyzerVerifier.Verify(SimulationSource.Outside("var a = DateTime.UtcNow; var b = new Random(); var c = Guid.NewGuid();"));

    [Fact]
    public Task IgnoresMethodsThatAreNeverRegistered()
        => AnalyzerVerifier.Verify(
            SimulationSource.MethodGroup("").Replace(
                "private static void Tick(TickContext<S> ctx) {  }",
                "private static void Tick(TickContext<S> ctx) {  }\n    private static void Other() { var v = DateTime.UtcNow; }"));

    [Fact]
    public Task ReportsRegisteredMethodGroupButNotItsNeighbours()
        => AnalyzerVerifier.Verify(
            SimulationSource.MethodGroup("var v = {|ESIM001:DateTime.UtcNow|};").Replace(
                "private static void Tick(",
                "private static void Other() { var v = DateTime.UtcNow; }\n    private static void Tick("));

    [Fact]
    public Task StaysSilentWhenEpochSimIsNotReferenced()
    {
        var test = new Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerTest<EpochSim.Analyzers.DeterminismAnalyzer, DefaultVerifier>
        {
            TestCode = "using System; public static class C { public static void Run() { var v = DateTime.UtcNow; } }",
            ReferenceAssemblies = SimulationSource.Net10
        };

        return test.RunAsync();
    }

    [Theory]
    [InlineData("var d = new Dictionary<int, int>(); {|#0:foreach (var p in d) { }|}")]
    [InlineData("var h = new HashSet<int>(); {|#0:foreach (var p in h) { }|}")]
    [InlineData("var d = new Dictionary<int, int>(); var v = {|#0:d.Select(p => p.Key)|};")]
    public async Task HashCollectionEnumeration_IsHiddenUnlessRaisedInEditorConfig(string statement)
    {
        const string enable = "root = true\n\n[*.cs]\ndotnet_diagnostic.ESIM006.severity = warning\n";

        await AnalyzerVerifier.Verify(
            SimulationSource.Lambda(statement),
            enable,
            new DiagnosticResult("ESIM006", DiagnosticSeverity.Warning).WithLocation(0));

        await AnalyzerVerifier.Verify(
            SimulationSource.Lambda(statement),
            expected: new DiagnosticResult("ESIM006", DiagnosticSeverity.Hidden).WithLocation(0));
    }

    [Fact]
    public Task RulesCanBeDisabledThroughEditorConfig()
        => AnalyzerVerifier.Verify(
            SimulationSource.Lambda("var v = DateTime.UtcNow;"),
            "root = true\n\n[*.cs]\ndotnet_diagnostic.ESIM001.severity = none\n");

    [Fact]
    public Task RulesCarryHelpLinkAndMessage()
    {
        var descriptor = new EpochSim.Analyzers.DeterminismAnalyzer().SupportedDiagnostics.Single(d => d.Id == "ESIM002");

        Assert.Equal("EpochSim.Determinism", descriptor.Category);
        Assert.Equal(DiagnosticSeverity.Warning, descriptor.DefaultSeverity);
        Assert.Contains("determinism.md", descriptor.HelpLinkUri, StringComparison.Ordinal);

        return AnalyzerVerifier.Verify(
            SimulationSource.Lambda("var v = {|#0:Random.Shared|};"),
            expected: new DiagnosticResult(descriptor).WithLocation(0)
                .WithArguments("Random.Shared", "Use ctx.Rng instead of Random.Shared"));
    }

    private static string StripMarkup(string statement)
        => MarkupPattern().Replace(statement, "$1");

    [GeneratedRegex(@"\{\|ESIM\d+:(.*?)\|\}")]
    private static partial Regex MarkupPattern();
}
