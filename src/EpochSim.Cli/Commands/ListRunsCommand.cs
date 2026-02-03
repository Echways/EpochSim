using EpochSim.Cli.App;
using EpochSim.Cli.Parsing;

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
            .OrderByDescending(dir => dir.Name)
            .Take(Math.Max(1, limit))
            .ToArray();

        Console.WriteLine($"Root={ctx.Root}");
        Console.WriteLine("RunId | HasMeta | HasEvents | HasStateFp | Snapshots | Dumps | MinRepros");

        foreach (var dir in dirs)
        {
            var runId = dir.Name;
            var meta = File.Exists(Path.Combine(dir.FullName, "meta.txt"));
            var eventsJsonl = Path.Combine(dir.FullName, "events.jsonl");
            var events = File.Exists(eventsJsonl) || File.Exists(eventsJsonl + ".gz");
            var statefp = File.Exists(Path.Combine(dir.FullName, "statefp.jsonl"));
            var snaps = Directory.Exists(Path.Combine(dir.FullName, "snapshots"))
                ? Directory.EnumerateFiles(Path.Combine(dir.FullName, "snapshots"), "snapshot-*.json").Count()
                : 0;
            var dumps = Directory.Exists(Path.Combine(dir.FullName, "dumps"))
                ? Directory.EnumerateFiles(Path.Combine(dir.FullName, "dumps"), "violation-meta-*.txt").Count()
                : 0;
            var minrepros = Directory.Exists(Path.Combine(dir.FullName, "minrepro"))
                ? Directory.GetDirectories(Path.Combine(dir.FullName, "minrepro")).Length
                : 0;

            Console.WriteLine($"{runId} | {(meta ? "Y" : "N")} | {(events ? "Y" : "N")} | {(statefp ? "Y" : "N")} | {snaps} | {dumps} | {minrepros}");
        }

        return 0;
    }
}
