using EpochSim.Kernel.Messaging;
using EpochSim.Kernel.Time;
using EpochSim.Serialization.EventLog;

namespace EpochSim.Execution.Middleware;

public sealed class EventLogMiddleware(EventLogWriter writer, IEventCodecV2 codec) : IExecutionMiddleware
{
    public void OnEventDispatched(SimTime time, IEvent ev)
    {
        if (!codec.TryEncode(ev, out var kind, out var payloadJson))
        {
            throw new InvalidOperationException(
                $"Event log encoding failed for event '{ev.GetType().FullName}'. " +
                "Why: the configured codec cannot encode this event type. " +
                "Fix: register this event in your codec builder, for example: " +
                "new JsonEventCodecBuilder().Register<YourEvent>().Build().");
        }

        if (!string.Equals(ev.Kind, kind, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Event log kind mismatch for '{ev.GetType().FullName}'. " +
                $"Event.Kind='{ev.Kind}', CodecKind='{kind}'. " +
                "Why: replay relies on stable kind names. " +
                "Fix: keep kind mapping consistent via [MessageKind(\"...\")] and codec registration.");
        }

        writer.Write(time.Tick, kind, payloadJson);
    }
}
