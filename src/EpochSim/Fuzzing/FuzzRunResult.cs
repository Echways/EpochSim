namespace EpochSim.Fuzzing;

internal sealed record FuzzRunResult<TState>(
    TState State,
    Exception? Error,
    long LastTick,
    IReadOnlyList<(long Tick, string Hash)> Fingerprints)
{
    public string ErrorSignature => Error is null ? string.Empty : $"{Error.GetType().FullName}|{Error.Message}";
}
