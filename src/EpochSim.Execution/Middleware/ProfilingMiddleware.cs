using System.Diagnostics;
using EpochSim.Kernel.Time;
using EpochSim.Observability.Profiling;

namespace EpochSim.Execution.Middleware;

public sealed class ProfilingMiddleware(InMemoryProfileSink sink) : IExecutionMiddleware
{
    private readonly Dictionary<string, long> _systemStartTicks = new();

    public void OnSystemTickStart(SimTime time, string systemName)
        => _systemStartTicks[systemName] = Stopwatch.GetTimestamp();

    public void OnSystemTickEnd(SimTime time, string systemName)
    {
        if (_systemStartTicks.TryGetValue(systemName, out var startTick))
        {
            var end = Stopwatch.GetTimestamp();
            sink.Emit(new ProfileRecord(time, systemName, end - startTick));
        }
    }
}
