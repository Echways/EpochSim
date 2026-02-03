namespace EpochSim.Execution.RunArtifacts;

public sealed record RunManifest(
    string EngineVersion,
    ulong Seed,
    long StartTick,
    long EndTick,
    int EventLogVersion,
    long SnapshotEvery,
    long FingerprintEvery,
    int MaxPumpStepsPerTick,
    int MaxEventsPerTick,
    bool StrictReplay);
