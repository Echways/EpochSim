using EpochSim.Kernel.Time;
using EpochSim.Kernel.Validation;

namespace EpochSim.Fuzzing;

internal sealed class PredicateInvariant<TState>(string name, Func<TState, bool> predicate) : IInvariant<TState>
{
    public string Name => name;

    public bool Check(SimTime time, TState state, out string message)
    {
        var holds = predicate(state);
        message = holds ? string.Empty : "predicate returned false";
        return holds;
    }
}
