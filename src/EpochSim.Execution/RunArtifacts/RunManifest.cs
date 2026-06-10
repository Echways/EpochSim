namespace EpochSim.Execution.RunArtifacts;

public sealed record RunManifest(
    string EngineVersion,
    string RunMode,
    ulong Seed,
    long StartTick,
    long EndTick,
    int EventLogVersion,
    string RngVersion,
    long SnapshotEvery,
    long FingerprintEvery,
    int MaxPumpStepsPerTick,
    int MaxEventsPerTick,
    bool StrictReplay,
    DateTime BuildTimestampUtc,
    string? Domain = null);
