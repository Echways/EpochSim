namespace EpochSim.Serialization.State;

public enum StateChangeKind { Added, Removed, Changed }

public readonly record struct StateChange(string Path, StateChangeKind Kind, string? Before, string? After);
