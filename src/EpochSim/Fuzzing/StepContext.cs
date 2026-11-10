using EpochSim.Kernel.Determinism;

namespace EpochSim.Fuzzing;

public readonly struct StepContext
{
    public static readonly DateTimeOffset ClockStart = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
    public static readonly TimeSpan TickDuration = TimeSpan.FromSeconds(1);

    internal StepContext(IRng rng, long tick, TimeProvider timeProvider)
    {
        Rng = rng;
        Tick = tick;
        TimeProvider = timeProvider;
    }

    public IRng Rng { get; }
    public long Tick { get; }
    public TimeProvider TimeProvider { get; }
}
