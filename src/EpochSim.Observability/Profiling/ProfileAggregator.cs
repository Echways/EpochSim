namespace EpochSim.Observability.Profiling;

public static class ProfileAggregator
{
    public static ProfileSummary Summarize(IEnumerable<ProfileRecord> records, int topN = 10)
    {
        var groups = records
            .GroupBy(r => r.SystemName)
            .Select(g =>
            {
                var arr = g.Select(x => x.ElapsedStopwatchTicks).OrderBy(x => x).ToArray();
                var count = arr.LongLength;
                var total = arr.Sum();
                var min = arr[0];
                var max = arr[^1];
                var p50 = Percentile(arr, 0.50);
                var p95 = Percentile(arr, 0.95);

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
        return new ProfileSummary(totalRecords, groups);
    }

    private static long Percentile(long[] sorted, double p)
    {
        if (sorted.Length == 0) return 0;
        if (p <= 0) return sorted[0];
        if (p >= 1) return sorted[^1];

        var n = sorted.Length;
        var idx = (int)Math.Ceiling(p * n) - 1;
        if (idx < 0) idx = 0;
        if (idx >= n) idx = n - 1;
        return sorted[idx];
    }

    private static long Sum(this long[] arr)
    {
        long s = 0;
        for (int i = 0; i < arr.Length; i++) s += arr[i];
        return s;
    }
}
