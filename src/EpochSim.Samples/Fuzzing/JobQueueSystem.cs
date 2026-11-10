using EpochSim.Kernel.Systems;

namespace EpochSim.Samples.Fuzzing;

public sealed class JobQueueSystem : ISystem<JobQueueState>
{
    private const int DuplicateAcknowledgementOdds = 20_000;

    public string Name => "JobQueue";

    public void Tick(TickContext<JobQueueState> ctx)
    {
        ctx.State.Pending += ctx.Rng.NextInt(0, 2);

        if (ctx.State.Pending > 0 && ctx.Rng.NextInt(0, 4) != 0)
            Complete(ctx.State);

        if (ctx.Rng.NextInt(0, DuplicateAcknowledgementOdds) == 0)
            Complete(ctx.State);
    }

    private static void Complete(JobQueueState state)
    {
        state.Pending--;
        state.Completed++;
    }
}
