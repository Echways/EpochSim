using EpochSim.Execution.RunArtifacts;
using EpochSim.Serialization.Snapshots;

namespace EpochSim.Execution.Diagnostics;

public static class PrettyRunInspector
{
    public static void Print(RunPaths paths)
    {
        Console.WriteLine($"RunDir={paths.RunDir}");
        Console.WriteLine($"RunId={paths.RunId}");
        Console.WriteLine();

        if (File.Exists(paths.MetaPath))
        {
            Console.WriteLine("Meta:");
            Console.WriteLine(File.ReadAllText(paths.MetaPath).TrimEnd());
            Console.WriteLine();
        }
        else
        {
            Console.WriteLine("Meta: missing");
            Console.WriteLine();
        }

        if (File.Exists(paths.ManifestPath))
        {
            Console.WriteLine("Manifest:");
            Console.WriteLine(File.ReadAllText(paths.ManifestPath).TrimEnd());
            Console.WriteLine();
        }
        else
        {
            Console.WriteLine("Manifest: missing");
            Console.WriteLine();
        }

        var eventsPath = paths.ResolveEventsPath();
        var tracePath = paths.ResolveTracePath();
        var profilePath = paths.ResolveProfilePath();
        PrintFileInfo(Path.GetFileName(eventsPath), eventsPath);
        PrintFileInfo(Path.GetFileName(tracePath), tracePath);
        PrintFileInfo(Path.GetFileName(profilePath), profilePath);
        PrintFileInfo("statefp.jsonl", paths.StateFpPath);
        Console.WriteLine();

        if (Directory.Exists(paths.SnapshotsDir))
        {
            var snapshots = Directory.EnumerateFiles(paths.SnapshotsDir, "snapshot-*.json").ToArray();
            Console.WriteLine($"Snapshots={snapshots.Length}");
            if (snapshots.Length > 0)
            {
                var latestSnapshot = snapshots.Select(p => new FileInfo(p))
                    .OrderByDescending(f => f.Name, NumericOrderingStringComparer.Instance)
                    .First();
                Console.WriteLine($"LastSnapshot={latestSnapshot.Name}");

                try
                {
                    var snap = SnapshotReader.Read(latestSnapshot.FullName);
                    Console.WriteLine($"LastSnapshotTick={snap.Tick}");
                }
                catch (Exception)
                {
                    Console.WriteLine("LastSnapshotTick=<unreadable>");
                }
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
                var latestDump = dumpMetas.Select(p => new FileInfo(p))
                    .OrderByDescending(f => f.Name, NumericOrderingStringComparer.Instance)
                    .First();
                Console.WriteLine($"LastDumpMeta={latestDump.Name}");
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

        Console.WriteLine();

        if (File.Exists(paths.StateFpPath))
        {
            var last = ReadLastNonEmptyLine(paths.StateFpPath);
            if (last is not null)
                Console.WriteLine($"LastStateFpLine={last}");
        }
    }

    private static void PrintFileInfo(string label, string path)
    {
        if (!File.Exists(path))
        {
            Console.WriteLine($"{label}: missing");
            return;
        }

        var fi = new FileInfo(path);
        var lines = SafeCountLines(path);

        Console.WriteLine($"{label}: size={fi.Length} bytes, lines={lines}");
    }

    private static long SafeCountLines(string path)
    {
        long n = 0;
        using var stream = OpenReadStream(path);
        using var reader = new StreamReader(stream);
        while (reader.ReadLine() is not null)
            n++;
        return n;
    }

    private static string? ReadLastNonEmptyLine(string path)
    {
        string? last = null;
        using var stream = OpenReadStream(path);
        using var reader = new StreamReader(stream);
        while (reader.ReadLine() is { } line)
        {
            if (!string.IsNullOrWhiteSpace(line))
                last = line;
        }
        return last;
    }

    private static Stream OpenReadStream(string path)
    {
        var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
            return new System.IO.Compression.GZipStream(fileStream, System.IO.Compression.CompressionMode.Decompress);
        return fileStream;
    }
}
