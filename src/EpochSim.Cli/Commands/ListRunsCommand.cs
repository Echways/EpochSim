using EpochSim.Cli.App;
using EpochSim.Cli.Parsing;
using EpochSim.Execution.RunArtifacts;

namespace EpochSim.Cli.Commands;

public sealed class ListRunsCommand : ICliCommand
{
    public int Execute(CommandContext ctx, string[] args)
    {
        var limit = args.Length > 0 && CliParsing.TryParseInt(args[0], out var limitArg) ? limitArg : 20;

        if (!Directory.Exists(ctx.Root))
        {
            Console.WriteLine($"Artifacts root not found: {ctx.Root}");
            return 2;
        }

        var dirs = Directory.GetDirectories(ctx.Root)
            .Select(path => new DirectoryInfo(path))
            .OrderByDescending(dir => dir.Name, NumericOrderingStringComparer.Instance)
            .Take(Math.Max(1, limit))
            .ToArray();

        Console.WriteLine($"Root={ctx.Root}");
        Console.WriteLine("RunId | HasMeta | HasEvents | HasTrace | HasProfile | HasStateFp | Snapshots | Dumps | MinRepros");

        foreach (var dir in dirs)
        {
            var runId = dir.Name;
            var meta = File.Exists(Path.Combine(dir.FullName, RunPaths.MetaFileName));
            var eventsJsonl = Path.Combine(dir.FullName, RunPaths.EventsFileName);
            var events = File.Exists(eventsJsonl) || File.Exists(eventsJsonl + ".gz");
            var traceJsonl = Path.Combine(dir.FullName, RunPaths.TraceFileName);
            var trace = File.Exists(traceJsonl) || File.Exists(traceJsonl + ".gz");
            var profileJsonl = Path.Combine(dir.FullName, RunPaths.ProfileFileName);
            var profile = File.Exists(profileJsonl) || File.Exists(profileJsonl + ".gz");
            var statefp = File.Exists(Path.Combine(dir.FullName, RunPaths.StateFingerprintFileName));
            var snaps = Directory.Exists(Path.Combine(dir.FullName, RunPaths.SnapshotsDirectoryName))
                ? Directory.EnumerateFiles(Path.Combine(dir.FullName, RunPaths.SnapshotsDirectoryName), "snapshot-*.json").Count()
                : 0;
            var dumps = Directory.Exists(Path.Combine(dir.FullName, RunPaths.DumpsDirectoryName))
                ? Directory.EnumerateFiles(Path.Combine(dir.FullName, RunPaths.DumpsDirectoryName), "violation-meta-*.txt").Count()
                : 0;
            var minrepros = Directory.Exists(Path.Combine(dir.FullName, "minrepro"))
                ? Directory.GetDirectories(Path.Combine(dir.FullName, "minrepro")).Length
                : 0;

            Console.WriteLine($"{runId} | {(meta ? "Y" : "N")} | {(events ? "Y" : "N")} | {(trace ? "Y" : "N")} | {(profile ? "Y" : "N")} | {(statefp ? "Y" : "N")} | {snaps} | {dumps} | {minrepros}");
        }

        return 0;
    }
}
