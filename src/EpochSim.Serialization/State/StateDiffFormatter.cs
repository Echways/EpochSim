namespace EpochSim.Serialization.State;

public static class StateDiffFormatter
{
    public static IReadOnlyList<string> Format(IReadOnlyList<StateChange> changes, int maxLines = 50)
    {
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLines);

        var lines = new List<string>(Math.Min(changes.Count, maxLines) + 1);

        for (var i = 0; i < changes.Count && i < maxLines; i++)
            lines.Add(FormatChange(changes[i]));

        if (changes.Count > maxLines)
            lines.Add($"... and {changes.Count - maxLines} more changes");

        return lines;
    }

    private static string FormatChange(StateChange change)
        => change.Kind switch
        {
            StateChangeKind.Added => $"{change.Path}: added {change.After}",
            StateChangeKind.Removed => $"{change.Path}: removed {change.Before}",
            _ => $"{change.Path}: {change.Before} -> {change.After}"
        };
}
