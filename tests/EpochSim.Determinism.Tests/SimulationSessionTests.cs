using EpochSim.Execution;
using EpochSim.Kernel.Time;
using EpochSim.Samples.Population;

public sealed class SimulationSessionTests
{
    [Fact]
    public void SessionRunUntil_MatchesRunTicks_ForDeterministicScenario()
    {
        var runTicksState = new WorldState();
        var runTicksEngine = CreateEngine();
        runTicksEngine.RunTicks(runTicksState, seed: 12345, start: SimTime.Zero, endInclusive: new SimTime(60));

        var sessionState = new WorldState();
        var sessionEngine = CreateEngine();

        using (var session = sessionEngine.CreateSession(sessionState, seed: 12345, start: SimTime.Zero))
        {
            session.RunUntil(new SimTime(60));
            Assert.Equal(61, session.CurrentTime.Tick);
        }

        Assert.Equal(runTicksState.Population, sessionState.Population);
        Assert.Equal(runTicksState.Fires, sessionState.Fires);
    }

    [Fact]
    public void TickOnce_AdvancesCurrentTime()
    {
        var state = new WorldState();
        var engine = CreateEngine();

        using var session = engine.CreateSession(state, seed: 12345, start: SimTime.Zero);

        Assert.True(session.TickOnce());
        Assert.Equal(1, session.CurrentTime.Tick);

        Assert.True(session.TickOnce());
        Assert.Equal(2, session.CurrentTime.Tick);
    }

    private static SimulationEngine<WorldState> CreateEngine()
    {
        var engine = new SimulationEngine<WorldState>();
        engine.AddSystem(new PopulationSystem());
        engine.RegisterCommandHandler(new GrowPopulationHandler());
        engine.RegisterCommandHandler(new ScheduleFireHandler());
        return engine;
    }
}
