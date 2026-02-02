using EpochSim.Kernel.Messaging;
using EpochSim.Kernel.Time;
using EpochSim.Serialization.EventLog;

namespace EpochSim.Execution.Middleware;

public sealed class EventLogMiddleware(EventLogWriter writer, IEventCodec codec) : IExecutionMiddleware
{
    public void OnEventDispatched(SimTime time, IEvent ev)
    {
        if (!codec.TryEncode(ev, out var kind, out var payload))
            throw new InvalidOperationException($"No codec for event {ev.GetType().Name}");

        writer.Write(time.Tick, kind, payload);
    }
}
