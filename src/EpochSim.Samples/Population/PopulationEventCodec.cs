using System.Globalization;
using System.Text.Json;
using EpochSim.Kernel.Messaging;
using EpochSim.Kernel.Time;
using EpochSim.Serialization.EventLog;

namespace EpochSim.Samples.Population;

public sealed class PopulationEventCodec : IEventCodecV2
{
    public bool TryEncode(IEvent ev, out string kind, out string payloadJson)
    {
        switch (ev)
        {
            case PopulationDeltaEvent pd:
                kind = "PopulationDelta";
                payloadJson = pd.Delta.ToString(CultureInfo.InvariantCulture);
                return true;

            case FireEvent f:
                kind = "Fire";
                payloadJson = f.Damage.ToString(CultureInfo.InvariantCulture);
                return true;

            case FireScheduledEvent fs:
                kind = "FireScheduled";
                payloadJson = $"{{\"at\":{fs.At.Tick.ToString(CultureInfo.InvariantCulture)},\"damage\":{fs.Damage.ToString(CultureInfo.InvariantCulture)}}}";
                return true;

            default:
                kind = "";
                payloadJson = "";
                return false;
        }
    }

    public bool TryDecode(string kind, string payloadJson, out IEvent ev)
    {
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;

            switch (kind)
            {
                case "PopulationDelta":
                    if (!TryReadInt(root, out var delta))
                        return Fail(out ev);
                    ev = new PopulationDeltaEvent(delta);
                    return true;

                case "Fire":
                    if (!TryReadInt(root, out var damage))
                        return Fail(out ev);
                    ev = new FireEvent(damage);
                    return true;

                case "FireScheduled":
                    if (root.ValueKind == JsonValueKind.Object)
                    {
                        if (!root.TryGetProperty("at", out var atEl) ||
                            !root.TryGetProperty("damage", out var dmgEl) ||
                            !TryReadLong(atEl, out var scheduledAt) ||
                            !TryReadInt(dmgEl, out var scheduledDamage))
                            return Fail(out ev);

                        ev = new FireScheduledEvent(new SimTime(scheduledAt), scheduledDamage);
                        return true;
                    }

                    if (root.ValueKind == JsonValueKind.String)
                    {
                        var payload = root.GetString() ?? "";
                        var parts = payload.Split('|');
                        if (parts.Length != 2)
                            return Fail(out ev);

                        if (!long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var scheduledAt))
                            return Fail(out ev);

                        if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var scheduledDamage))
                            return Fail(out ev);

                        ev = new FireScheduledEvent(new SimTime(scheduledAt), scheduledDamage);
                        return true;
                    }

                    return Fail(out ev);

                default:
                    return Fail(out ev);
            }
        }
        catch (JsonException)
        {
            return Fail(out ev);
        }
    }

    private static bool TryReadInt(JsonElement el, out int value)
    {
        if (el.ValueKind == JsonValueKind.Number)
            return el.TryGetInt32(out value);

        if (el.ValueKind == JsonValueKind.String)
            return int.TryParse(el.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

        value = default;
        return false;
    }

    private static bool TryReadLong(JsonElement el, out long value)
    {
        if (el.ValueKind == JsonValueKind.Number)
            return el.TryGetInt64(out value);

        if (el.ValueKind == JsonValueKind.String)
            return long.TryParse(el.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

        value = default;
        return false;
    }

    private static bool Fail(out IEvent ev)
    {
        ev = default!;
        return false;
    }
}
