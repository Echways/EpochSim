using EpochSim.Execution;
using EpochSim.Kernel.Time;
using EpochSim.Observability.Tracing;
using EpochSim.Samples.Population;
using Xunit;

public sealed class DeterminismTests
{
    [Fact]
    public void SameSeed_ProducesSameResultAndTrace()
    {
        var (pop1, fires1, fp1) = Run(seed: 12345);
        var (pop2, fires2, fp2) = Run(seed: 12345);

        Assert.Equal(pop1, pop2);
        Assert.Equal(fires1, fires2);
        Assert.Equal(fp1, fp2);
    }

    private static (int pop, int fires, string fingerprint) Run(ulong seed)
    {
        var engine = new SimulationEngine<WorldState>();
        engine.AddSystem(new PopulationSystem());

        engine.RegisterCommandHandler(new GrowPopulationHandler());
        engine.RegisterCommandHandler(new ScheduleFireHandler());

        var sink = new InMemoryTraceSink();
        engine.AddMiddleware(new EpochSim.Execution.Middleware.TraceMiddleware(sink));

        var world = new WorldState();
        engine.RunTicks(world, seed, SimTime.Zero, new SimTime(60));

        var fingerprint = TraceFingerprint.Compute(sink.Records);
        return (world.Population, world.Fires, fingerprint);
    }
}
