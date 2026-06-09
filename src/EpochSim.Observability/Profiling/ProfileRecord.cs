using EpochSim.Kernel.Time;

namespace EpochSim.Observability.Profiling;

public readonly record struct ProfileRecord(
    SimTime Time,
    string SystemName,
    long ElapsedStopwatchTicks);
