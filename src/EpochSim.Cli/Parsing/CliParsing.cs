using EpochSim.Execution.RunArtifacts;

namespace EpochSim.Cli.Parsing;

public static class CliParsing
{
    public static bool IsOption(string s) => s.StartsWith("--", StringComparison.Ordinal);

    public static string NormalizeRunId(string s)
    {
        s = (s ?? "").Trim();
        s = s.TrimEnd('/', '\\');
        if (s.Contains(Path.DirectorySeparatorChar) || s.Contains(Path.AltDirectorySeparatorChar))
            return Path.GetFileName(s);
        return s;
    }

    public static (string? RunId, int NextIndex) ParseRunIdIfPresent(string[] args)
    {
        if (args.Length <= 0)
            return (null, 0);

        var firstArg = args[0];

        if (IsOption(firstArg))
            return (null, 0);

        if (long.TryParse(firstArg, out _))
            return (null, 0);

        if (int.TryParse(firstArg, out _))
            return (null, 0);

        if (ulong.TryParse(firstArg, out _))
            return (null, 0);

        return (NormalizeRunId(firstArg), 1);
    }

    public static HashSet<string>? ParseKindsOptions(string[] args, int startIndex)
    {
        HashSet<string>? kinds = null;

        for (var i = startIndex; i < args.Length; i++)
        {
            var arg = args[i];
            if (!IsOption(arg)) continue;

            if (string.Equals(arg, "--only", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "--kind", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length) continue;
                var value = args[i + 1];
                i++;

                if (string.IsNullOrWhiteSpace(value)) continue;

                kinds ??= new HashSet<string>(StringComparer.Ordinal);

                foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    kinds.Add(part);
            }
        }

        return kinds;
    }

    public static string ResolveLatestRunId(string root)
    {
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException($"Artifacts root not found: {root}");

        var dirs = Directory.GetDirectories(root)
            .Select(path => new DirectoryInfo(path))
            .OrderByDescending(dir => dir.Name, NumericOrderingStringComparer.Instance)
            .ToArray();

        if (dirs.Length == 0)
            throw new InvalidOperationException($"No runs found in {root}");

        return dirs[0].Name;
    }

    public static string ResolveRunIdWithEvents(string root, string runArg)
    {
        runArg = (runArg ?? "").Trim();

        if (!string.IsNullOrWhiteSpace(runArg))
        {
            var id = NormalizeRunId(runArg);
            var dir = Path.Combine(root, id);
            var events = Path.Combine(dir, RunPaths.EventsFileName);
            var eventsGz = events + ".gz";
            if (!File.Exists(events) && !File.Exists(eventsGz))
                throw new FileNotFoundException($"events log not found for run: {dir}");
            return id;
        }

        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException($"Artifacts root not found: {root}");

        var dirs = Directory.GetDirectories(root)
            .Select(path => new DirectoryInfo(path))
            .OrderByDescending(dir => dir.Name, NumericOrderingStringComparer.Instance);

        foreach (var dir in dirs)
        {
            var events = Path.Combine(dir.FullName, RunPaths.EventsFileName);
            var eventsGz = events + ".gz";
            if (File.Exists(events) || File.Exists(eventsGz))
                return dir.Name;
        }

        throw new InvalidOperationException($"No runs with events logs found in {root}");
    }

    public static string ResolveLatestMinRepro(string minRoot)
    {
        var dirs = Directory.GetDirectories(minRoot)
            .Select(path => new DirectoryInfo(path))
            .OrderByDescending(dir => dir.Name, NumericOrderingStringComparer.Instance)
            .ToArray();

        if (dirs.Length == 0)
            throw new InvalidOperationException($"No minrepro dirs found in {minRoot}");

        return dirs[0].FullName;
    }

    public static RunPaths Paths(string root, string runId) => new RunPaths(root, runId);

    public static bool TryParseLong(string? s, out long v) => long.TryParse(s, out v);
    public static bool TryParseInt(string? s, out int v) => int.TryParse(s, out v);
    public static bool TryParseUlong(string? s, out ulong v) => ulong.TryParse(s, out v);
}
