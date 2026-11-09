using EpochSim.Serialization.State;

namespace EpochSim.Cli.Commands;

internal static class StateDiffReport
{
    public static IReadOnlyList<string> Describe(string title, string beforeJson, string afterJson)
    {
        var lines = new List<string> { title };
        var diff = StateDiffFormatter.Format(StateDiff.Compute(beforeJson, afterJson));

        if (diff.Count == 0)
            lines.Add("  (no field changes)");
        else
            lines.AddRange(diff.Select(line => $"  {line}"));

        return lines;
    }
}
