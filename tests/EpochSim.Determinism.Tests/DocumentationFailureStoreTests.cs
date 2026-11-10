using EpochSim;
using EpochSim.Cli.App;
using EpochSim.Samples.Onboarding;
using MyApp;

[Collection("Console")]
public sealed class DocumentationFailureStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"epochsim-doc-store-{Guid.NewGuid():N}");

    public DocumentationFailureStoreTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Theory]
    [InlineData("docs/en/fuzzing.md")]
    [InlineData("docs/ru/fuzzing.md")]
    public async Task FuzzingDocument_ShowsTheRealSecondRunAndTheGeneratedRegressionTest(string document)
    {
        var storePath = Path.Combine(_directory, "epochsim-failures.json");
        Epoch.Fuzz<Inventory>(fuzz => InventoryFuzz.Configure(fuzz.FailureStore(storePath)));
        var secondRun = Epoch.Fuzz<Inventory>(fuzz => InventoryFuzz.Configure(fuzz.FailureStore(storePath)));

        var exitCode = await ExportTest(storePath);

        var documentText = File.ReadAllText(Path.Combine(RepositoryRoot(), document)).ReplaceLineEndings("\n");
        var generated = File.ReadAllText(Path.Combine(_directory, "tests", "InventoryFuzzRegressionTests.cs")).ReplaceLineEndings("\n");
        Assert.Equal(0, exitCode);
        Assert.Contains(KnownFailureBlock(secondRun.ToText()), documentText, StringComparison.Ordinal);
        Assert.Contains(generated.TrimEnd(), documentText, StringComparison.Ordinal);
    }

    private static string KnownFailureBlock(string reportText)
    {
        var text = reportText.ReplaceLineEndings("\n");
        var start = text.IndexOf("[InvariantViolation]", StringComparison.Ordinal);
        var end = text.IndexOf("\n\n", start, StringComparison.Ordinal);
        return text[start..end];
    }

    private async Task<int> ExportTest(string storePath)
    {
        var originalOut = Console.Out;
        Console.SetOut(TextWriter.Null);

        try
        {
            return await new CliApp().RunAsync(
            [
                "failures", "export-test",
                "--framework", "xunit",
                "--configure", "MyApp.InventoryFuzz.Configure",
                "--out", Path.Combine(_directory, "tests"),
                "--store", storePath
            ]);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EpochSim.slnx")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException("EpochSim.slnx was not found above the test binaries.");
    }
}
