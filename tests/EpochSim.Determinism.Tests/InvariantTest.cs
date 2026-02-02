using EpochSim.Execution;
using EpochSim.Execution.Middleware;
using EpochSim.Kernel.Time;
using EpochSim.Kernel.Validation;
using EpochSim.Samples.Population;
using Xunit;

public sealed class InvariantTests
{
    [Fact]
    public void InvariantMiddleware_CatchesViolation()
    {
        var engine = new SimulationEngine<WorldState>();
        engine.AddSystem(new PopulationSystem());

        engine.RegisterCommandHandler(new GrowPopulationHandler());
        engine.RegisterCommandHandler(new ScheduleFireHandler());

        var world = new WorldState { Population = -1 };

        var invariants = new List<IInvariant<WorldState>>
        {
            new PopulationNonNegativeInvariant()
        };

        engine.AddMiddleware(new InvariantMiddleware<WorldState>(world, invariants));

        Assert.Throws<InvariantViolationException>(() =>
            engine.RunTicks(world, seed: 12345, start: SimTime.Zero, endInclusive: new SimTime(1)));
    }
}
