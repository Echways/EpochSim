using System.Threading;
using EpochSim.Execution;
using EpochSim.Execution.Middleware;
using EpochSim.Kernel.Time;

namespace EpochSim;

public static class SimulationEngineExtensions
{
    extension<TState>(SimulationEngine<TState> engine)
    {
        public void Attach(EpochSimRunScope<TState> run)
            => run.AttachTo(engine);

        public void RunWith(
            EpochSimRunScope<TState> run,
            TState state,
            ulong seed,
            SimTime start,
            SimTime endInclusive,
            RunOptions? options = null,
            CancellationToken cancellationToken = default)
            => engine.RunTicks(state, seed, start, endInclusive, options, run.Context, cancellationToken);
    }

    extension<TState>(EpochSimRunScope<TState> run)
    {
        public IExecutionMiddleware CompositeMiddleware => run.Middleware;
    }
}
