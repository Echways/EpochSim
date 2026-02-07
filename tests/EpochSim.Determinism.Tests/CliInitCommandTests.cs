using EpochSim.Cli.App;

public sealed class CliInitCommandTests
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
            Assert.True(File.Exists(Path.Combine(targetDir, "EpochSim.App.csproj")));

            var program = File.ReadAllText(Path.Combine(targetDir, "Program.cs"));
            Assert.Contains("Epoch.QuickRun", program, StringComparison.Ordinal);

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
}
