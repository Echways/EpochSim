using System.Text.Json;
using EpochSim.Kernel.Messaging;

namespace EpochSim.Serialization.EventLog;

public sealed class EventCodecV2Adapter(IEventCodec inner) : IEventCodecV2
{
    public bool TryEncode(IEvent ev, out string kind, out string payloadJson)
    {
        if (!inner.TryEncode(ev, out kind, out var payload))
        {
            payloadJson = "";
            return false;
        }

        payloadJson = JsonSerializer.Serialize(payload);
        return true;
    }

    public bool TryDecode(string kind, string payloadJson, out IEvent ev)
    {
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            if (doc.RootElement.ValueKind != JsonValueKind.String)
                return Fail(out ev);

            var payload = doc.RootElement.GetString() ?? "";
            return inner.TryDecode(kind, payload, out ev);
        }
        catch (JsonException)
        {
            return Fail(out ev);
        }
    }

    private static bool Fail(out IEvent ev)
    {
        ev = default!;
        return false;
    }
}
