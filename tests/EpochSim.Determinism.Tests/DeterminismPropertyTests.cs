using EpochSim.Execution;
using EpochSim.Execution.Middleware;
using EpochSim.Execution.StateFingerprint;
using EpochSim.Kernel.Messaging;
using EpochSim.Kernel.Systems;
using EpochSim.Kernel.Time;
using EpochSim.Serialization.State;

public sealed class DeterminismPropertyTests
{
    private const int MetaSeed = 0xEA7051;

    [Fact]
    public void RandomSystemGraph_SameSeed_ProducesIdenticalFingerprintSequences()
    {
        var meta = new Random(MetaSeed);

        for (var trial = 0; trial < 20; trial++)
        {
            var simSeed = (ulong)meta.NextInt64(1, long.MaxValue);
            var systemCount = meta.Next(1, 6);
            var tickCount = meta.Next(5, 30);
            var scheduleProbabilities = Enumerable.Range(0, systemCount)
                .Select(_ => meta.NextDouble() * 0.4)
                .ToArray();

            var fp1 = RunAndCollectFingerprints(simSeed, systemCount, tickCount, scheduleProbabilities);
            var fp2 = RunAndCollectFingerprints(simSeed, systemCount, tickCount, scheduleProbabilities);

            Assert.True(fp1.SequenceEqual(fp2),
                $"Trial {trial}: seed={simSeed} systems={systemCount} ticks={tickCount} — fingerprint sequences diverged");
        }
    }

    private static IReadOnlyList<string> RunAndCollectFingerprints(
        ulong seed,
        int systemCount,
        int tickCount,
        double[] scheduleProbabilities)
    {
        var engine = new SimulationEngine<CounterArray>();

        for (var i = 0; i < systemCount; i++)
        {
            var idx = i;
            var p = scheduleProbabilities[i];
            engine.AddSystem(
                name: $"sys-{idx}",
                tick: ctx =>
                {
                    ctx.State.Counts[idx]++;
                    if (ctx.Rng.NextDouble01() < p)
                        ctx.Scheduler.ScheduleNextTick(new PulseEvent(idx));
                },
                handle: (ctx, ev) =>
                {
                    if (ev is PulseEvent pulse)
                        ctx.State.Pulses[pulse.Source]++;
                });
        }

        var state = new CounterArray(systemCount);
        var serializer = new JsonStateSerializer<CounterArray>();
        var sink = new InMemoryStateFingerprintSink();
        engine.AddMiddleware(new StateFingerprintMiddleware<CounterArray>(state, serializer, sink, intervalTicks: 1));

        engine.RunTicks(state, seed, SimTime.Zero, new SimTime(tickCount));

        return sink.Records.Select(r => r.Hash ?? "").ToList();
    }

    private sealed class CounterArray(int n)
    {
        public int[] Counts { get; } = new int[n];
        public int[] Pulses { get; } = new int[n];
    }

    private sealed record PulseEvent(int Source) : IEvent;
}
