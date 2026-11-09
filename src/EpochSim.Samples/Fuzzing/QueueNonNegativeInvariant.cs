using EpochSim.Kernel.Time;
using EpochSim.Kernel.Validation;

namespace EpochSim.Samples.Fuzzing;

public sealed class QueueNonNegativeInvariant : IInvariant<JobQueueState>
{
    public string Name => "QueueNonNegative";

    public bool Check(SimTime time, JobQueueState state, out string message)
    {
        if (state.Pending >= 0)
        {
            message = string.Empty;
            return true;
        }

        message = $"Pending={state.Pending}";
        return false;
    }
}
