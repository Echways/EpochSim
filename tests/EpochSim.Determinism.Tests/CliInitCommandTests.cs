using System.Reflection;
using System.Text.RegularExpressions;
using EpochSim.Cli.App;
using EpochSim.Fuzzing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

public sealed partial class CliInitCommandTests
{
    [Fact]
    public async Task InitCommand_ScaffoldsMinimalProject()
    {
        var targetDir = Path.Combine(Path.GetTempPath(), $"epochsim-init-{Guid.NewGuid():N}");

        try
        {
            var app = new CliApp();

            var rc = await app.RunAsync(["init", targetDir]);
            Assert.Equal(0, rc);

            Assert.True(File.Exists(Path.Combine(targetDir, "Program.cs")));
            Assert.True(File.Exists(Path.Combine(targetDir, "WorldState.cs")));
            Assert.True(File.Exists(Path.Combine(targetDir, "SessionInboxExample.cs")));
            Assert.True(File.Exists(Path.Combine(targetDir, "WorldFuzz.cs")));
            Assert.True(File.Exists(Path.Combine(targetDir, "EpochSim.App.csproj")));

            var program = File.ReadAllText(Path.Combine(targetDir, "Program.cs"));
            Assert.Contains("Epoch.QuickRun", program, StringComparison.Ordinal);
            Assert.Contains("run.RunTicks(engine", program, StringComparison.Ordinal);
            Assert.DoesNotContain("engine.RunTicks(state", program, StringComparison.Ordinal);

            var csproj = File.ReadAllText(Path.Combine(targetDir, "EpochSim.App.csproj"));
            Assert.Contains($"Version=\"{PackageVersion()}\"", csproj, StringComparison.Ordinal);
            Assert.Matches(VersionPattern(), PackageVersion());

            var inboxExample = File.ReadAllText(Path.Combine(targetDir, "SessionInboxExample.cs"));
            Assert.Contains("CommandInbox", inboxExample, StringComparison.Ordinal);
            Assert.Contains("CreateSession", inboxExample, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(targetDir))
                Directory.Delete(targetDir, recursive: true);
        }
    }

    [Fact]
    public async Task InitCommand_ScaffoldsAFuzzCallThatCompilesAndRuns()
    {
        var targetDir = Path.Combine(Path.GetTempPath(), $"epochsim-init-{Guid.NewGuid():N}");

        try
        {
            Assert.Equal(0, await new CliApp().RunAsync(["init", targetDir]));

            var fuzz = File.ReadAllText(Path.Combine(targetDir, "WorldFuzz.cs"));
            Assert.Contains("Epoch.Fuzz<WorldState>", fuzz, StringComparison.Ordinal);
            Assert.Contains(".Step(", fuzz, StringComparison.Ordinal);
            Assert.Contains(".Invariant(", fuzz, StringComparison.Ordinal);
            Assert.DoesNotContain("//", fuzz, StringComparison.Ordinal);

            var program = File.ReadAllText(Path.Combine(targetDir, "Program.cs"));
            Assert.Contains("WorldFuzz.Run()", program, StringComparison.Ordinal);

            var assembly = Compile(Directory.GetFiles(targetDir, "*.cs"));
            var report = (FuzzReport)assembly.GetType("WorldFuzz")!.GetMethod("Run")!.Invoke(null, null)!;
            Assert.True(report.Passed, report.ToText());
            Assert.Equal(1000, report.SeedsRun);
        }
        finally
        {
            if (Directory.Exists(targetDir))
                Directory.Delete(targetDir, recursive: true);
        }
    }

    private const string ImplicitUsings =
        "global using System; global using System.Collections.Generic; global using System.IO; " +
        "global using System.Linq; global using System.Threading; global using System.Threading.Tasks;";

    private static string PackageVersion()
        => typeof(EpochSim.Epoch).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion.Split('+')[0];

    private static Assembly Compile(IEnumerable<string> files)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));

        var compilation = CSharpCompilation.Create(
            $"Scaffold{Guid.NewGuid():N}",
            files.Select(file => CSharpSyntaxTree.ParseText(File.ReadAllText(file), new CSharpParseOptions(LanguageVersion.Latest)))
                .Append(CSharpSyntaxTree.ParseText(ImplicitUsings, new CSharpParseOptions(LanguageVersion.Latest))),
            references,
            new CSharpCompilationOptions(OutputKind.ConsoleApplication, nullableContextOptions: NullableContextOptions.Enable));

        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        var problems = result.Diagnostics.Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning).ToArray();
        Assert.True(result.Success && problems.Length == 0, string.Join(Environment.NewLine, problems.Select(problem => problem.ToString())));

        return Assembly.Load(stream.ToArray());
    }

    [GeneratedRegex(@"^\d+\.\d+\.\d+$")]
    private static partial Regex VersionPattern();
}
