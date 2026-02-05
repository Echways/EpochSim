using EpochSim.Execution;
using EpochSim.Execution.Middleware;
using EpochSim.Execution.RunArtifacts;
using EpochSim.Observability.Profiling;
using EpochSim.Observability.Tracing;

namespace EpochSim.Hosting;

internal sealed record EpochSimRunScopeInit<TState>(
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

internal sealed class DeferredTraceExport
{
    public JsonlTraceWriter? Writer { get; set; }
}

internal sealed class DeferredProfileExport
{
    public JsonlProfileWriter? Writer { get; set; }
}

internal sealed class TraceRuntime(InMemoryTraceSink sink, DeferredTraceExport export)
{
    public void FlushToWriter()
    {
        if (export.Writer is null)
            return;

        var records = sink.Records;
        for (var i = 0; i < records.Count; i++)
            export.Writer.Write(records[i]);

        export.Writer.Flush();
    }
}

internal sealed class ProfileRuntime(InMemoryProfileSink sink, DeferredProfileExport export)
{
    public void FlushToWriter()
    {
        if (export.Writer is null)
            return;

        var records = sink.Records;
        for (var i = 0; i < records.Count; i++)
            export.Writer.Write(records[i]);

        export.Writer.Flush();
    }
}
