using EpochSim.Kernel.Messaging;
using EpochSim.Kernel.Time;
using EpochSim.Serialization.EventLog;

namespace EpochSim.Execution.Middleware;

public sealed class EventLogMiddleware(EventLogWriter writer, IEventCodecV2 codec) : IExecutionMiddleware
{
    public void OnEventDispatched(SimTime time, IEvent ev)
    {
        if (!codec.TryEncode(ev, out var kind, out var payloadJson))
            throw new InvalidOperationException($"No codec for event {ev.GetType().Name}");

        if (!string.Equals(ev.Kind, kind, StringComparison.Ordinal))
            throw new InvalidOperationException($"Event kind mismatch: ev.Kind={ev.Kind}, codec={kind}.");

        writer.Write(time.Tick, kind, payloadJson);
    }
}
