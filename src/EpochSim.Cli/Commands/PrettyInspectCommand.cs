using EpochSim.Cli.App;
using EpochSim.Cli.Parsing;
using EpochSim.Execution.RunArtifacts;

namespace EpochSim.Cli.Commands;

public sealed class PrettyInspectCommand : ICliCommand
{
    public int Execute(CommandContext ctx, string[] args)
    {
        var runArg = args.Length > 0 ? args[0] : "";
        var runId = string.IsNullOrWhiteSpace(runArg) ? CliParsing.ResolveLatestRunId(ctx.Root) : CliParsing.NormalizeRunId(runArg);
        var paths = new RunPaths(ctx.Root, runId);

        if (!Directory.Exists(paths.RunDir))
            throw new DirectoryNotFoundException($"RunDir not found: {paths.RunDir}");

        EpochSim.Execution.Diagnostics.PrettyRunInspector.Print(paths);
        return 0;
    }
}
