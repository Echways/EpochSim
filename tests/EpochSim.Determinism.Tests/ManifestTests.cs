using System.Text.Json;
using System.Threading;
using EpochSim.Cli.App;
using EpochSim.Cli.Commands;
using EpochSim.Cli.Domain;
using Xunit;

public sealed class ManifestTests
{
    [Fact]
    public void RunCommand_WritesManifest()
    {
        var root = Path.Combine(Path.GetTempPath(), $"epochsim-manifest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var ctx = new CommandContext(root, DomainAdapterRegistry.Default, CancellationToken.None);
            var cmd = new RunCommand();

            var runId = "testrun";
            var rc = cmd.Execute(ctx, new[] { runId, "1", "1", "12345" });
            Assert.Equal(0, rc);

            var manifestPath = Path.Combine(root, runId, "manifest.json");
            Assert.True(File.Exists(manifestPath));

            using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var rootEl = doc.RootElement;
            Assert.True(rootEl.TryGetProperty("EngineVersion", out _));
            Assert.True(rootEl.TryGetProperty("RunMode", out _));
            Assert.True(rootEl.TryGetProperty("RngVersion", out _));
            Assert.True(rootEl.TryGetProperty("BuildTimestampUtc", out _));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
