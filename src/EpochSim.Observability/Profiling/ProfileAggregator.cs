namespace EpochSim.Observability.Profiling;

public static class ProfileAggregator
{
    public static ProfileSummary Summarize(IEnumerable<ProfileRecord> records, int topN = 10)
    {
        var rows = records
            .GroupBy(r => r.SystemName)
            .Select(g =>
            {
                var samples = g.Select(x => x.ElapsedStopwatchTicks).OrderBy(x => x).ToArray();
                var count = samples.LongLength;
                var total = samples.Sum();
                var min = samples[0];
                var max = samples[^1];
                var p50 = Percentile(samples, 0.50);
                var p95 = Percentile(samples, 0.95);

                return new ProfileSummaryRow(
                    SystemName: g.Key,
                    Count: count,
                    TotalStopwatchTicks: total,
                    MinStopwatchTicks: min,
                    P50StopwatchTicks: p50,
                    P95StopwatchTicks: p95,
                    MaxStopwatchTicks: max);
            })
            .OrderByDescending(r => r.TotalStopwatchTicks)
            .Take(Math.Max(1, topN))
            .ToArray();

        var totalRecords = records.LongCount();
        return new ProfileSummary(totalRecords, rows);
    }

    private static long Percentile(long[] sorted, double percentile)
    {
        if (sorted.Length == 0) return 0;
        if (percentile <= 0) return sorted[0];
        if (percentile >= 1) return sorted[^1];

        var n = sorted.Length;
        var idx = (int)Math.Ceiling(percentile * n) - 1;
        if (idx < 0) idx = 0;
        if (idx >= n) idx = n - 1;
        return sorted[idx];
    }

    private static long Sum(this long[] values)
    {
        long total = 0;
        for (int i = 0; i < values.Length; i++) total += values[i];
        return total;
    }
}
