using System.Reflection;
using System.Text.RegularExpressions;
using EpochSim;
using EpochSim.Samples.Fuzzing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

[CollectionDefinition("Snippets", DisableParallelization = true)]
public sealed class SnippetsCollection;

[Collection("Snippets")]
public sealed partial class DocumentationSnippetTests : IDisposable
{
    private const string ImplicitUsings =
        "global using System; global using System.Collections.Generic; global using System.IO; " +
        "global using System.Linq; global using System.Threading; global using System.Threading.Tasks;";

    private static readonly string[] Topics = ["artifacts", "determinism", "embedding", "fuzzing", "sessions", "testing"];

    private static readonly string[] Documents =
    [
        "README.md",
        "README.ru.md",
        .. Topics.Select(topic => $"docs/en/{topic}.md"),
        .. Topics.Select(topic => $"docs/ru/{topic}.md")
    ];

    private static readonly (string English, string Russian)[] Translations =
    [
        ("README.md", "README.ru.md"),
        .. Topics.Select(topic => ($"docs/en/{topic}.md", $"docs/ru/{topic}.md"))
    ];

    private readonly string _workingDirectory = Path.Combine(Path.GetTempPath(), $"epochsim-snippet-{Guid.NewGuid():N}");
    private readonly string _originalDirectory = Directory.GetCurrentDirectory();

    public DocumentationSnippetTests()
    {
        Directory.CreateDirectory(_workingDirectory);
        Directory.SetCurrentDirectory(_workingDirectory);
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_originalDirectory);
        Directory.Delete(_workingDirectory, recursive: true);
    }

    public static TheoryData<string, int> Snippets()
    {
        var data = new TheoryData<string, int>();

        foreach (var document in Documents)
        {
            foreach (var snippet in ReadSnippets(document))
                data.Add(document, snippet.Number);
        }

        return data;
    }

    public static TheoryData<string, string> TranslationPairs()
    {
        var data = new TheoryData<string, string>();

        foreach (var (english, russian) in Translations)
            data.Add(english, russian);

        return data;
    }

    [Theory]
    [MemberData(nameof(TranslationPairs))]
    public void EnglishAndRussianDocumentsHaveTheSameSnippets(string english, string russian)
    {
        var englishSnippets = ReadSnippets(english);
        var russianSnippets = ReadSnippets(russian);

        Assert.Equal(englishSnippets.Count, russianSnippets.Count);
        Assert.Equal(
            englishSnippets.Select(snippet => (snippet.ExpectedException, snippet.CompileOnly)),
            russianSnippets.Select(snippet => (snippet.ExpectedException, snippet.CompileOnly)));
    }

    [Theory]
    [MemberData(nameof(Snippets))]
    public void Snippet_CompilesAndRuns(string document, int number)
    {
        var snippet = ReadSnippets(document).Single(candidate => candidate.Number == number);
        var assembly = Compile(snippet, document);

        if (snippet.CompileOnly)
            return;

        var entryPoint = assembly.EntryPoint
                         ?? throw new Xunit.Sdk.XunitException($"{document} snippet #{number} has no top-level statements to run.");

        var originalOut = Console.Out;
        Console.SetOut(TextWriter.Null);

        try
        {
            var outcome = Record.Exception(() => Invoke(entryPoint));
            Assert.True(snippet.ExpectedException == outcome?.GetType().Name, outcome?.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void ReadmeFuzzSnippetFindsTheInventoryBugAndReportsTheFailingSeed()
    {
        var snippet = ReadSnippets("README.md").Single(candidate => candidate.ExpectedException == "FuzzFailedException");
        var assembly = Compile(snippet, "README.md");

        var failure = Record.Exception(() => Invoke(assembly.EntryPoint!));

        var exception = Assert.IsType<EpochSim.Fuzzing.FuzzFailedException>(failure);
        Assert.Contains("StockNonNegative", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Failures=21, SeedsRun=1000", exception.Message, StringComparison.Ordinal);
        Assert.Contains(
            "[InvariantViolation] seed=18 tick=19\n  StockNonNegative: predicate returned false\n  StateDiff:\n    $.Stock: 1 -> -2\n  Repro: .Seeds(18, 18).Ticks(19)",
            exception.Message.ReplaceLineEndings("\n"),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("docs/en/fuzzing.md")]
    [InlineData("docs/ru/fuzzing.md")]
    public void FuzzingDocument_ShowsTheRealOutputOfTheShrinkingExample(string document)
    {
        var report = Epoch.Fuzz<AccountState>(f => f
            .State(() => new AccountState())
            .Engine(AccountLedger.Register)
            .Invariants(() => [new BalanceNonNegativeInvariant()])
            .Commands(AccountLedger.PlanCommands)
            .Seeds(1, 3)
            .Ticks(500));

        var documentText = File.ReadAllText(Path.Combine(RepositoryRoot(), document)).ReplaceLineEndings("\n");

        Assert.Contains(report.Failures[0].ToText().ReplaceLineEndings("\n"), documentText, StringComparison.Ordinal);
    }

    private static void Invoke(MethodInfo entryPoint)
    {
        try
        {
            entryPoint.Invoke(null, entryPoint.GetParameters().Length == 0 ? null : [Array.Empty<string>()]);
        }
        catch (TargetInvocationException error) when (error.InnerException is { } inner)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(inner).Throw();
        }
    }

    private static Assembly Compile(Snippet snippet, string document)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));

        var options = new CSharpParseOptions(LanguageVersion.Latest);
        var compilation = CSharpCompilation.Create(
            $"Snippet{Guid.NewGuid():N}",
            [CSharpSyntaxTree.ParseText(snippet.Code, options), CSharpSyntaxTree.ParseText(ImplicitUsings, options)],
            references,
            new CSharpCompilationOptions(
                snippet.CompileOnly ? OutputKind.DynamicallyLinkedLibrary : OutputKind.ConsoleApplication,
                nullableContextOptions: NullableContextOptions.Enable));

        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        var errors = result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.True(result.Success, $"{document} snippet #{snippet.Number} does not compile:{Environment.NewLine}{string.Join(Environment.NewLine, errors.Select(error => error.ToString()))}");

        return Assembly.Load(stream.ToArray());
    }

    private static List<Snippet> ReadSnippets(string relativePath)
    {
        var lines = File.ReadAllLines(Path.Combine(RepositoryRoot(), relativePath));
        var snippets = new List<Snippet>();

        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Trim() != "```csharp")
                continue;

            var end = Array.FindIndex(lines, i + 1, line => line.Trim() == "```");
            var marker = i > 0 ? ExpectationMarker().Match(lines[i - 1]) : Match.Empty;
            snippets.Add(new Snippet(
                snippets.Count + 1,
                string.Join(Environment.NewLine, lines[(i + 1)..end]),
                marker.Success && marker.Groups["exception"].Success ? marker.Groups["exception"].Value : null,
                marker.Success && marker.Groups["compiles"].Success));
            i = end;
        }

        return snippets;
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EpochSim.slnx")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException("EpochSim.slnx was not found above the test binaries.");
    }

    [GeneratedRegex(@"^<!-- snippet: (?:throws (?<exception>\w+)|(?<compiles>compiles)) -->$")]
    private static partial Regex ExpectationMarker();

    private sealed record Snippet(int Number, string Code, string? ExpectedException, bool CompileOnly);
}
