namespace EpochSim.Observability.Profiling;

public sealed record ProfileSummaryRow(
    string SystemName,
    long Count,
    long TotalStopwatchTicks,
    long MinStopwatchTicks,
    long P50StopwatchTicks,
    long P95StopwatchTicks,
    long MaxStopwatchTicks);

public sealed record ProfileSummary(
    long TotalRecords,
    IReadOnlyList<ProfileSummaryRow> Rows);
