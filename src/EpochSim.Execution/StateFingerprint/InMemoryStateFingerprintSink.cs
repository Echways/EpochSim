namespace EpochSim.Execution.StateFingerprint;

public sealed class InMemoryStateFingerprintSink : IStateFingerprintSink
{
    private readonly List<(long Tick, string Hash)> _records = new();
    public IReadOnlyList<(long Tick, string Hash)> Records => _records;

    public void OnRecord(long tick, string hash) => _records.Add((tick, hash));
}
