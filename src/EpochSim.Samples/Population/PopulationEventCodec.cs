using System.Globalization;
using System.Text.Json;
using EpochSim.Kernel.Messaging;
using EpochSim.Kernel.Time;
using EpochSim.Serialization.EventLog;

namespace EpochSim.Samples.Population;

public sealed class PopulationEventCodec : IEventCodecV2
{
    private static readonly IEventCodecV2 Inner = new JsonEventCodecBuilder()
        .Register<PopulationDeltaEvent>()
        .Register<FireEvent>()
        .Register<FireScheduledEvent>()
        .Build();

    public bool TryEncode(IEvent ev, out string kind, out string payloadJson)
        => Inner.TryEncode(ev, out kind, out payloadJson);

    public bool TryDecode(string kind, string payloadJson, out IEvent ev)
    {
        if (Inner.TryDecode(kind, payloadJson, out ev))
            return true;

        if (string.Equals(kind, "FireScheduled", StringComparison.Ordinal))
            return TryDecodeLegacyFireScheduled(payloadJson, out ev);

        return TryDecodeLegacyInt(kind, payloadJson, out ev);
    }

    private static bool TryDecodeLegacyInt(string kind, string payloadJson, out IEvent ev)
    {
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.String)
                return Fail(out ev);

            var raw = root.GetString() ?? "";
            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                return Fail(out ev);

            if (string.Equals(kind, "PopulationDelta", StringComparison.Ordinal))
            {
                ev = new PopulationDeltaEvent(value);
                return true;
            }

            if (string.Equals(kind, "Fire", StringComparison.Ordinal))
            {
                ev = new FireEvent(value);
                return true;
            }

            return Fail(out ev);
        }
        catch (JsonException)
        {
            return Fail(out ev);
        }
    }

    private static bool TryDecodeLegacyFireScheduled(string payloadJson, out IEvent ev)
    {
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            if (doc.RootElement.ValueKind != JsonValueKind.String)
                return Fail(out ev);

            var payload = doc.RootElement.GetString() ?? "";
            var parts = payload.Split('|');
            if (parts.Length != 2)
                return Fail(out ev);

            if (!long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var at))
                return Fail(out ev);

            if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var damage))
                return Fail(out ev);

            ev = new FireScheduledEvent(new SimTime(at), damage);
            return true;
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
