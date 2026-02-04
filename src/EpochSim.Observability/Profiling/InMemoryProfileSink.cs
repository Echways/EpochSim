namespace EpochSim.Observability.Profiling;

public sealed class InMemoryProfileSink
{
    private readonly List<ProfileRecord> _records = new();

    public IReadOnlyList<ProfileRecord> Records
    {
        get
        {
            lock (_records)
            {
                return _records.ToArray();
            }
        }
    }

    public void Emit(in ProfileRecord r)
    {
        lock (_records)
        {
            _records.Add(r);
        }
    }
}
