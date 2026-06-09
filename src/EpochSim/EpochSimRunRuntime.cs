using EpochSim.Execution;
using EpochSim.Execution.Middleware;
using EpochSim.Execution.RunArtifacts;
using EpochSim.Observability.Profiling;
using EpochSim.Observability.Tracing;

namespace EpochSim;

internal sealed record RunScopeInit<TState>(
    TState State,
    RunPaths Paths,
    RunContext Context,
    IExecutionMiddleware Middleware,
    RunCaptureMiddleware Capture,
    IReadOnlyList<IDisposable> Resources,
    TraceRuntime? TraceRuntime,
    ProfileRuntime? ProfileRuntime,
    long SnapshotEveryTicks,
    long FingerprintEveryTicks,
    bool HasEventLog,
    RunMode Mode);

internal sealed class TraceRuntime(InMemoryTraceSink sink, JsonlTraceWriter writer)
{
    public void FlushToWriter()
    {
        var records = sink.Records;
        for (var i = 0; i < records.Count; i++)
            writer.Write(records[i]);

        writer.Flush();
    }
}

internal sealed class ProfileRuntime(InMemoryProfileSink sink, JsonlProfileWriter writer)
{
    public void FlushToWriter()
    {
        var records = sink.Records;
        for (var i = 0; i < records.Count; i++)
            writer.Write(records[i]);

        writer.Flush();
    }
}
