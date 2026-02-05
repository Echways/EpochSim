using EpochSim.Kernel.Determinism;

namespace EpochSim.Execution;

public sealed class RunOptions
{
    public int MaxPumpStepsPerTick
    {
        get;
        init
        {
            if (value <= 0)
                throw new InvalidOperationException($"MaxPumpStepsPerTick must be > 0 (value={value}).");

            field = value;
        }
    } = 1024;

    public int MaxEventsPerTick
    {
        get;
        init
        {
            if (value <= 0)
                throw new InvalidOperationException($"MaxEventsPerTick must be > 0 (value={value}).");

            field = value;
        }
    } = 100_000;

    public RngVersion RngVersion { get; init; } = RngVersion.V2;

    public void Validate()
    {
        _ = MaxPumpStepsPerTick;
        _ = MaxEventsPerTick;
    }
}
