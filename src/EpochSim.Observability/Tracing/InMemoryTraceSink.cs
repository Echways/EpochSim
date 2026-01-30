namespace EpochSim.Observability.Tracing;

public sealed class InMemoryTraceSink
{
    private readonly List<TraceRecord> _records = [];

    public IReadOnlyList<TraceRecord> Records
    {
        get
        {
            lock (_records)
            {
                return _records.ToArray();
            }
        }
    }

    public void Emit(in TraceRecord r)
    {
        lock (_records)
        {
            _records.Add(r);
        }
    }
}
