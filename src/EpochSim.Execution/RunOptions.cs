using EpochSim.Kernel.Determinism;

namespace EpochSim.Execution;

public sealed class RunOptions
{
    // Prevents infinite command/event chains from locking the engine.
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

    // Prevents explosive event fan-out. Hard engine-level cap (distinct from domain invariants).
    public int MaxEventDispatchesPerTick
    {
        get;
        init
        {
            if (value <= 0)
                throw new InvalidOperationException($"MaxEventDispatchesPerTick must be > 0 (value={value}).");

            field = value;
        }
    } = 100_000;

    [Obsolete("Use MaxEventDispatchesPerTick instead.")]
    public int MaxEventsPerTick
    {
        get => MaxEventDispatchesPerTick;
        init => MaxEventDispatchesPerTick = value;
    }

    // V1 kept for backward compatibility with older runs.
    public RngVersion RngVersion { get; init; } = RngVersion.V2;
}
