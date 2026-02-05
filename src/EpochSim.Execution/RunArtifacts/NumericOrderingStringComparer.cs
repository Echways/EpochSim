using System.Globalization;

namespace EpochSim.Execution.RunArtifacts;

public sealed class NumericOrderingStringComparer : IComparer<string?>
{
    public static NumericOrderingStringComparer Instance { get; } = new();

    private static readonly CompareInfo CompareInfo = CultureInfo.InvariantCulture.CompareInfo;
    private const CompareOptions Options = CompareOptions.NumericOrdering | CompareOptions.IgnoreCase;

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y))
            return 0;

        if (x is null)
            return -1;

        if (y is null)
            return 1;

        return CompareInfo.Compare(x, y, Options);
    }
}
