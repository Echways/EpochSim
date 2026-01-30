namespace EpochSim.Serialization.Snapshots;

public readonly record struct SnapshotFile(long Tick, string StateJson);