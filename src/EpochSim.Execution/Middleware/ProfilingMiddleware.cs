using System.Diagnostics;
using EpochSim.Execution.Middleware;
using EpochSim.Kernel.Time;
using EpochSim.Observability.Profiling;

namespace EpochSim.Execution.Middleware;

public sealed class ProfilingMiddleware(InMemoryProfileSink sink) : IExecutionMiddleware
{
    private readonly Dictionary<string, long> _start = new();

    public void OnSystemTickStart(SimTime time, string systemName)
        => _start[systemName] = Stopwatch.GetTimestamp();

    public void OnSystemTickEnd(SimTime time, string systemName)
    {
        if (_start.TryGetValue(systemName, out var st))
        {
            var end = Stopwatch.GetTimestamp();
            sink.Emit(new ProfileRecord(time, systemName, end - st));
        }
    }
}