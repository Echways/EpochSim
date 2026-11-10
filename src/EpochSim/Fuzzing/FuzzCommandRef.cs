namespace EpochSim.Fuzzing;

public readonly record struct FuzzCommandRef(int Index, long Tick, string Type);
