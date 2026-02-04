using EpochSim.Kernel.Time;

namespace EpochSim.Execution;

public enum RunMode
{
    Run,
    Replay
}

public sealed record RunInfo(
    RunMode Mode,
    ulong Seed,
    SimTime StartTick,
    SimTime EndTickInclusive,
    string? RunId,
    string? ArtifactDir,
    RunOptions Options,
    bool StrictReplay,
    string RngVersion);

public sealed record RunContext(
    string? RunId,
    string? ArtifactDir);
