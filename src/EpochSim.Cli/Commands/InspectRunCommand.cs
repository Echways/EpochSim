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

        if (File.Exists(paths.ManifestPath))
        {
            Console.WriteLine("Manifest:");
            Console.WriteLine(File.ReadAllText(paths.ManifestPath));
        }
        else
        {
            Console.WriteLine("Manifest: missing");
        }

        var eventsPath = paths.ResolveEventsPath();
        if (File.Exists(eventsPath))
            Console.WriteLine($"EventsLines={CountLines(eventsPath)}");
        else
            Console.WriteLine("Events: missing");

        var profilePath = paths.ResolveProfilePath();
        if (File.Exists(profilePath))
            Console.WriteLine($"ProfileLines={CountLines(profilePath)}");
        else
            Console.WriteLine("Profile: missing");

        if (File.Exists(paths.StateFpPath))
            Console.WriteLine($"StateFpLines={File.ReadLines(paths.StateFpPath).LongCount()}");
        else
            Console.WriteLine("StateFp: missing");

        if (Directory.Exists(paths.SnapshotsDir))
        {
            var snapshots = Directory.EnumerateFiles(paths.SnapshotsDir, "snapshot-*.json").ToArray();
            Console.WriteLine($"Snapshots={snapshots.Length}");
            if (snapshots.Length > 0)
            {
                var lastSnapshot = snapshots.Select(p => new FileInfo(p))
                    .OrderByDescending(f => f.Name, NumericOrderingStringComparer.Instance)
                    .First();
                Console.WriteLine($"LastSnapshot={lastSnapshot.Name}");
            }
        }
        else
        {
            Console.WriteLine("Snapshots: missing");
        }

        if (Directory.Exists(paths.DumpsDir))
        {
            var dumpMetas = Directory.EnumerateFiles(paths.DumpsDir, "violation-meta-*.txt").ToArray();
            Console.WriteLine($"DumpMetas={dumpMetas.Length}");
            if (dumpMetas.Length > 0)
            {
                var lastDump = dumpMetas.Select(p => new FileInfo(p))
                    .OrderByDescending(f => f.Name, NumericOrderingStringComparer.Instance)
                    .First();
                Console.WriteLine($"LastDumpMeta={lastDump.Name}");
            }
        }
        else
        {
            Console.WriteLine("Dumps: missing");
        }

        var minDir = Path.Combine(paths.RunDir, "minrepro");
        if (Directory.Exists(minDir))
        {
            var repros = Directory.GetDirectories(minDir)
                .OrderByDescending(Path.GetFileName, NumericOrderingStringComparer.Instance)
                .ToArray();
            Console.WriteLine($"MinRepros={repros.Length}");
            if (repros.Length > 0)
                Console.WriteLine($"LastMinRepro={repros[0]}");
        }
        else
        {
            Console.WriteLine("MinRepros: none");
        }

        return 0;
    }

    private static long CountLines(string path)
    {
        using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        Stream stream = fileStream;
        if (path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
            stream = new System.IO.Compression.GZipStream(fileStream, System.IO.Compression.CompressionMode.Decompress);

        using var reader = new StreamReader(stream);
        long count = 0;
        while (reader.ReadLine() is not null)
            count++;
        return count;
    }
}
