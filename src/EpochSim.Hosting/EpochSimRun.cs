namespace EpochSim.Hosting;

public static class EpochSimRun
{
    public static EpochSimRunBuilder<TState> For<TState>(TState state)
        => new(state);
}
