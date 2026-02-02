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

        var a = args[0];

        if (IsOption(a))
            return (null, 0);

        if (long.TryParse(a, out _))
            return (null, 0);

        if (int.TryParse(a, out _))
            return (null, 0);

        if (ulong.TryParse(a, out _))
            return (null, 0);

        return (NormalizeRunId(a), 1);
    }

    public static HashSet<string>? ParseKindsOptions(string[] args, int startIndex)
    {
        HashSet<string>? set = null;

        for (var i = startIndex; i < args.Length; i++)
        {
            var a = args[i];
            if (!IsOption(a)) continue;

            if (string.Equals(a, "--only", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a, "--kind", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length) continue;
                var v = args[i + 1];
                i++;

                if (string.IsNullOrWhiteSpace(v)) continue;

                set ??= new HashSet<string>(StringComparer.Ordinal);

                foreach (var part in v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    set.Add(part);
            }
        }

        return set;
    }

    public static string ResolveLatestRunId(string root)
    {
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException($"Artifacts root not found: {root}");

        var dirs = Directory.GetDirectories(root)
            .Select(d => new DirectoryInfo(d))
            .OrderByDescending(d => d.Name)
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
            var events = Path.Combine(dir, "events.jsonl");
            if (!File.Exists(events))
                throw new FileNotFoundException($"events.jsonl not found for run: {dir}");
            return id;
        }

        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException($"Artifacts root not found: {root}");

        var dirs = Directory.GetDirectories(root)
            .Select(d => new DirectoryInfo(d))
            .OrderByDescending(d => d.Name);

        foreach (var d in dirs)
        {
            var events = Path.Combine(d.FullName, "events.jsonl");
            if (File.Exists(events))
                return d.Name;
        }

        throw new InvalidOperationException($"No runs with events.jsonl found in {root}");
    }

    public static string ResolveLatestMinRepro(string minRoot)
    {
        var dirs = Directory.GetDirectories(minRoot)
            .Select(d => new DirectoryInfo(d))
            .OrderByDescending(d => d.Name)
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
