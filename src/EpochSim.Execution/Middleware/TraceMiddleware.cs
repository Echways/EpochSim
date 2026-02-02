using EpochSim.Kernel.Messaging;
using EpochSim.Kernel.Time;
using EpochSim.Observability.Tracing;

namespace EpochSim.Execution.Middleware;

public sealed class TraceMiddleware(InMemoryTraceSink sink) : IExecutionMiddleware
{
    public void OnTickStart(SimTime time) => sink.Emit(new(time, "tick", "start"));
    public void OnTickEnd(SimTime time) => sink.Emit(new(time, "tick", "end"));

    public void OnEventDispatched(SimTime time, IEvent ev)
        => sink.Emit(new(time, "event", ev.Kind));

    public void OnSystemTickStart(SimTime time, string systemName)
        => sink.Emit(new(time, "system", systemName, Detail: "start"));

    public void OnSystemTickEnd(SimTime time, string systemName)
        => sink.Emit(new(time, "system", systemName, Detail: "end"));
}
