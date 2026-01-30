using EpochSim.Kernel.Time;
using EpochSim.Kernel.Validation;

namespace EpochSim.Samples.Population;

public sealed class PopulationNonNegativeInvariant : IInvariant<WorldState>
{
    public string Name => "PopulationNonNegative";

    public bool Check(SimTime time, WorldState state, out string message)
    {
        if (state.Population >= 0)
        {
            message = "";
            return true;
        }

        message = $"Population={state.Population}";
        return false;
    }
}
