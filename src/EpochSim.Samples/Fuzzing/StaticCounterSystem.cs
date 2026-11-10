using EpochSim.Kernel.Systems;

namespace EpochSim.Samples.Fuzzing;

public sealed class StaticCounterSystem : ISystem<StaticCounterState>
{
    private static long _shared;

    public string Name => "StaticCounter";

    public void Tick(TickContext<StaticCounterState> ctx)
        => ctx.State.Value += Interlocked.Increment(ref _shared);
}
