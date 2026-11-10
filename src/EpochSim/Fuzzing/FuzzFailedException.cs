namespace EpochSim.Fuzzing;

public sealed class FuzzFailedException(FuzzReport report) : Exception(report.ToText())
{
    public FuzzReport Report { get; } = report;
}
