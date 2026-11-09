using EpochSim.Execution.Middleware;
using EpochSim.Kernel.Time;

namespace EpochSim.Fuzzing;

internal sealed class FuzzTickProbe : IExecutionMiddleware
{
    public long LastTick { get; private set; }

    public void OnTickStart(SimTime time)
        => LastTick = time.Tick;
}
