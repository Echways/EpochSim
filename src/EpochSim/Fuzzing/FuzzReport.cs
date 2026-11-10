using System.Text;

namespace EpochSim.Fuzzing;

public sealed class FuzzReport
{
    internal FuzzReport(int seedsRun, TimeSpan duration, IReadOnlyList<FuzzFailure> failures)
    {
        SeedsRun = seedsRun;
        Duration = duration;
        Failures = failures;
    }

    public bool Passed => Failures.Count == 0;
    public int SeedsRun { get; }
    public TimeSpan Duration { get; }
    public IReadOnlyList<FuzzFailure> Failures { get; }

    public string ToText()
    {
        var elapsedMs = (long)Duration.TotalMilliseconds;
        if (Passed)
            return $"Fuzz passed: SeedsRun={SeedsRun}, ElapsedMs={elapsedMs}";

        var text = new StringBuilder();
        text.Append("Fuzz failed: Failures=").Append(Failures.Count)
            .Append(", SeedsRun=").Append(SeedsRun)
            .Append(", ElapsedMs=").Append(elapsedMs).AppendLine();

        foreach (var failure in Failures)
            text.AppendLine().AppendLine(failure.ToText());

        return text.ToString().TrimEnd();
    }

    public void ThrowIfFailed()
    {
        if (!Passed)
            throw new FuzzFailedException(this);
    }
}
