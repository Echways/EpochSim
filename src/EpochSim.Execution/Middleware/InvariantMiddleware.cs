using EpochSim.Execution.Validation;
using EpochSim.Kernel.Time;
using EpochSim.Kernel.Validation;

namespace EpochSim.Execution.Middleware;

public sealed class InvariantMiddleware<TState>(
    TState state,
    IReadOnlyList<IInvariant<TState>> invariants,
    long checkEveryTicks = 1) : IExecutionMiddleware
{
    public void OnTickEnd(SimTime time)
    {
        if (checkEveryTicks <= 0) return;
        if (time.Tick % checkEveryTicks != 0) return;

        for (int i = 0; i < invariants.Count; i++)
        {
            var inv = invariants[i];
            if (!inv.Check(time, state, out var msg))
                throw new InvariantViolationException(inv.Name, time, msg);
        }
    }
}
