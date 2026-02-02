using EpochSim.Cli.App;
using EpochSim.Cli.Parsing;
using EpochSim.Execution.RunArtifacts;

namespace EpochSim.Cli.Commands;

public sealed class InspectRunCommand : ICliCommand
{
    public int Execute(CommandContext ctx, string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("inspect-run требует runId или путь");

        var runId = CliParsing.NormalizeRunId(args[0]);
        var paths = new RunPaths(ctx.Root, runId);

        if (!Directory.Exists(paths.RunDir))
        {
            Console.WriteLine($"RunDir not found: {paths.RunDir}");
            return 2;
        }

        Console.WriteLine($"RunDir={paths.RunDir}");
        Console.WriteLine($"RunId={paths.RunId}");

        if (File.Exists(paths.MetaPath))
        {
            Console.WriteLine("Meta:");
            Console.WriteLine(File.ReadAllText(paths.MetaPath));
        }
        else
        {
            Console.WriteLine("Meta: missing");
        }

        if (File.Exists(paths.EventsPath))
            Console.WriteLine($"EventsLines={File.ReadLines(paths.EventsPath).LongCount()}");
        else
            Console.WriteLine("Events: missing");

        if (File.Exists(paths.StateFpPath))
            Console.WriteLine($"StateFpLines={File.ReadLines(paths.StateFpPath).LongCount()}");
        else
            Console.WriteLine("StateFp: missing");

        if (Directory.Exists(paths.SnapshotsDir))
        {
            var snaps = Directory.EnumerateFiles(paths.SnapshotsDir, "snapshot-*.json").ToArray();
            Console.WriteLine($"Snapshots={snaps.Length}");
            if (snaps.Length > 0)
            {
                var last = snaps.Select(p => new FileInfo(p)).OrderByDescending(f => f.Name).First();
                Console.WriteLine($"LastSnapshot={last.Name}");
            }
        }
        else
        {
            Console.WriteLine("Snapshots: missing");
        }

        if (Directory.Exists(paths.DumpsDir))
        {
            var dumps = Directory.EnumerateFiles(paths.DumpsDir, "violation-meta-*.txt").ToArray();
            Console.WriteLine($"DumpMetas={dumps.Length}");
            if (dumps.Length > 0)
            {
                var last = dumps.Select(p => new FileInfo(p)).OrderByDescending(f => f.Name).First();
                Console.WriteLine($"LastDumpMeta={last.Name}");
            }
        }
        else
        {
            Console.WriteLine("Dumps: missing");
        }

        var minDir = Path.Combine(paths.RunDir, "minrepro");
        if (Directory.Exists(minDir))
        {
            var reps = Directory.GetDirectories(minDir).OrderByDescending(x => x).ToArray();
            Console.WriteLine($"MinRepros={reps.Length}");
            if (reps.Length > 0)
                Console.WriteLine($"LastMinRepro={reps[0]}");
        }
        else
        {
            Console.WriteLine("MinRepros: none");
        }

        return 0;
    }
}
