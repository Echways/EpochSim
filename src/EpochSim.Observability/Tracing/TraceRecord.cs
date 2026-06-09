using EpochSim.Kernel.Time;

namespace EpochSim.Observability.Tracing;

public readonly record struct TraceRecord(
    SimTime Time,
    string Type,
    string Name,
    long? DurationTicks = null,
    string? Detail = null);
