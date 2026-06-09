using EpochSim.Kernel.Time;

namespace EpochSim.Kernel.Validation;

public interface IInvariant<TState>
{
    string Name { get; }
    bool Check(SimTime time, TState state, out string message);
}
