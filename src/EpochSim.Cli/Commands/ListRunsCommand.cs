using EpochSim.Cli.App;
using EpochSim.Cli.Parsing;

namespace EpochSim.Cli.Commands;

public sealed class ListRunsCommand : ICliCommand
{
    public int Execute(CommandContext ctx, string[] args)
    {
        var limit = args.Length > 0 && CliParsing.TryParseInt(args[0], out var l) ? l : 20;

        if (!Directory.Exists(ctx.Root))
        {
            Console.WriteLine($"Artifacts root not found: {ctx.Root}");
            return 2;
        }

        var dirs = Directory.GetDirectories(ctx.Root)
            .Select(d => new DirectoryInfo(d))
            .OrderByDescending(d => d.Name)
            .Take(Math.Max(1, limit))
            .ToArray();

        Console.WriteLine($"Root={ctx.Root}");
        Console.WriteLine("RunId | HasMeta | HasEvents | HasStateFp | Snapshots | Dumps | MinRepros");

        foreach (var d in dirs)
        {
            var runId = d.Name;
            var meta = File.Exists(Path.Combine(d.FullName, "meta.txt"));
            var events = File.Exists(Path.Combine(d.FullName, "events.jsonl"));
            var statefp = File.Exists(Path.Combine(d.FullName, "statefp.jsonl"));
            var snaps = Directory.Exists(Path.Combine(d.FullName, "snapshots"))
                ? Directory.EnumerateFiles(Path.Combine(d.FullName, "snapshots"), "snapshot-*.json").Count()
                : 0;
            var dumps = Directory.Exists(Path.Combine(d.FullName, "dumps"))
                ? Directory.EnumerateFiles(Path.Combine(d.FullName, "dumps"), "violation-meta-*.txt").Count()
                : 0;
            var minrepros = Directory.Exists(Path.Combine(d.FullName, "minrepro"))
                ? Directory.GetDirectories(Path.Combine(d.FullName, "minrepro")).Length
                : 0;

            Console.WriteLine($"{runId} | {(meta ? "Y" : "N")} | {(events ? "Y" : "N")} | {(statefp ? "Y" : "N")} | {snaps} | {dumps} | {minrepros}");
        }

        return 0;
    }
}