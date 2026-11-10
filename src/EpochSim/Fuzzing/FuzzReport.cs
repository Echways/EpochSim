using System.Text;

namespace EpochSim.Fuzzing;

public sealed class FuzzReport
{
    private readonly bool _usesFailureStore;

    internal FuzzReport(
        int seedsRun,
        TimeSpan duration,
        IReadOnlyList<FuzzFailure> failures,
        IReadOnlyList<ulong> fixedSeeds,
        bool usesFailureStore)
    {
        SeedsRun = seedsRun;
        Duration = duration;
        Failures = failures;
        FixedSeeds = fixedSeeds;
        _usesFailureStore = usesFailureStore;
    }

    public bool Passed => Failures.Count == 0;
    public int SeedsRun { get; }
    public TimeSpan Duration { get; }
    public IReadOnlyList<FuzzFailure> Failures { get; }
    public IReadOnlyList<ulong> FixedSeeds { get; }
    public IReadOnlyList<FuzzFailure> NewFailures => Failures.Where(failure => !failure.IsKnown).ToArray();
    public IReadOnlyList<FuzzFailure> KnownFailures => Failures.Where(failure => failure.IsKnown).ToArray();

    public string ToText()
    {
        var elapsedMs = (long)Duration.TotalMilliseconds;
        var text = new StringBuilder();

        if (Passed)
            text.Append($"Fuzz passed: SeedsRun={SeedsRun}, ElapsedMs={elapsedMs}");
        else
            text.Append("Fuzz failed: Failures=").Append(Failures.Count).Append(FailureBreakdown())
                .Append(", SeedsRun=").Append(SeedsRun)
                .Append(", ElapsedMs=").Append(elapsedMs);

        foreach (var seed in FixedSeeds)
            text.AppendLine().Append("Previously failing seed ").Append(seed).Append(" now passes.");

        if (_usesFailureStore)
        {
            AppendSection(text, "New failures:", NewFailures);
            AppendSection(text, "Known failures:", KnownFailures);
            return text.ToString();
        }

        foreach (var failure in Failures)
            text.AppendLine().AppendLine().Append(failure.ToText());

        return text.ToString();
    }

    public void ThrowIfFailed()
    {
        if (!Passed)
            throw new FuzzFailedException(this);
    }

    private string FailureBreakdown()
        => _usesFailureStore ? $" (new={NewFailures.Count}, known={KnownFailures.Count})" : string.Empty;

    private static void AppendSection(StringBuilder text, string title, IReadOnlyList<FuzzFailure> failures)
    {
        if (failures.Count == 0)
            return;

        text.AppendLine().AppendLine().Append(title);

        foreach (var failure in failures)
            text.AppendLine().AppendLine().Append(failure.ToText());
    }
}
