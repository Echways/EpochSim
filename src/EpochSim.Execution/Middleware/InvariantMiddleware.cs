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
            var invariant = invariants[i];
            if (!invariant.Check(time, state, out var message))
                throw new InvariantViolationException(invariant.Name, time, message);
        }
    }
}
