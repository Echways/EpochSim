using EpochSim.Kernel.Time;

namespace EpochSim.Execution.Validation;

public sealed class InvariantViolationException(string name, SimTime time, string message)
    : Exception($"Invariant violated: {name} at tick {time.Tick}: {message}")
{
    public string InvariantName { get; } = name;
    public SimTime Time { get; } = time;
    public string Detail { get; } = message;
}
