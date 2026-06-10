using EpochSim.Execution;
using EpochSim.Execution.Middleware;
using EpochSim.Execution.StateFingerprint;
using EpochSim.Kernel.Time;
using EpochSim.Serialization.State;
using Xunit;

public sealed class MiddlewareTickZeroTests
{
    private sealed class RecordingSink : IStateFingerprintSink
    {
        public List<long> Ticks { get; } = new();
        public void OnRecord(long tick, string fingerprint) => Ticks.Add(tick);
    }

    private sealed class SimpleState { public int Value { get; set; } }

    [Fact]
    public void StateFingerprintMiddleware_SkipsTick0_WhenIntervalIs1()
    {
        var state = new SimpleState();
        var serializer = new JsonStateSerializer<SimpleState>();
        var sink = new RecordingSink();
        var middleware = new StateFingerprintMiddleware<SimpleState>(state, serializer, sink, intervalTicks: 1);

        middleware.OnTickEnd(SimTime.Zero);       // tick 0 — must be skipped
        middleware.OnTickEnd(new SimTime(1));     // tick 1 — must be recorded

        Assert.DoesNotContain(0L, sink.Ticks);
        Assert.Contains(1L, sink.Ticks);
    }

    [Fact]
    public void SnapshotMiddleware_SkipsTick0()
    {
        var root = Path.Combine(Path.GetTempPath(), $"epochsim-snap-tick0-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var state = new SimpleState { Value = 42 };
            var serializer = new JsonStateSerializer<SimpleState>();
            var middleware = new SnapshotMiddleware<SimpleState>(state, intervalTicks: 1, root, serializer);

            middleware.OnTickEnd(SimTime.Zero);  // tick 0 — must produce no file
            middleware.OnTickEnd(new SimTime(1)); // tick 1 — must produce a file

            Assert.False(File.Exists(Path.Combine(root, "snapshot-0.json")));
            Assert.True(File.Exists(Path.Combine(root, "snapshot-1.json")));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
