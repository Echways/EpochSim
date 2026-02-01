using System.Text.Json;
using EpochSim.Execution.Diagnostics;

namespace EpochSim.Samples.Population;

public sealed class PopulationEventPayloadFormatter : IEventPayloadFormatter
{
    public bool TryFormat(string kind, string payload, out string formatted)
    {
        formatted = "";

        if (string.Equals(kind, "Fire", StringComparison.OrdinalIgnoreCase))
        {
            if (TryGetInt(payload, "Damage", out var dmg))
            {
                formatted = $"Damage={dmg}";
                return true;
            }

            formatted = payload ?? "";
            return true;
        }

        if (TryGetInt(payload, "Amount", out var amount))
        {
            formatted = $"Amount={amount}";
            return true;
        }

        if (TryGetInt(payload, "Delta", out var delta))
        {
            formatted = $"Delta={delta}";
            return true;
        }

        return false;
    }

    private static bool TryGetInt(string payload, string name, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(payload)) return false;

        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return false;

            if (!doc.RootElement.TryGetProperty(name, out var p)) return false;

            if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out value))
                return true;

            return false;
        }
        catch
        {
            return false;
        }
    }
}
