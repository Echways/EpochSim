using EpochSim.Kernel.Determinism;

namespace EpochSim.Execution;

public sealed class RunOptions
{
    public int MaxPumpStepsPerTick { get; init; } = 1024;
    public int MaxEventsPerTick { get; init; } = 100_000;
    public RngVersion RngVersion { get; init; } = RngVersion.V2;

    public void Validate()
    {
        if (MaxPumpStepsPerTick <= 0)
            throw new InvalidOperationException($"MaxPumpStepsPerTick must be > 0 (value={MaxPumpStepsPerTick}).");

        if (MaxEventsPerTick <= 0)
            throw new InvalidOperationException($"MaxEventsPerTick must be > 0 (value={MaxEventsPerTick}).");
    }
}
