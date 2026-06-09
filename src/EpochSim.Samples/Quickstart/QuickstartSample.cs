using EpochSim;

namespace EpochSim.Samples.Quickstart;

public static class QuickstartSample
{
    public static int RunPopulationDemo()
    {
        var state = new WorldState();
        var engine = Epoch.CreateEngine<WorldState>();

        engine.AddSystem("World", tick: ctx => ctx.State.Population++);

        using var run = Epoch.QuickRun(state, rootDir: "artifacts");
        run.RunTicks(engine, seed: 1, endTickInclusive: 100);

        return state.Population;
    }
}

public sealed class WorldState
{
    public int Population { get; set; }
}
