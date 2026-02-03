using System.Text.Json;
using EpochSim.Execution.Diagnostics;

namespace EpochSim.Samples.Population;

public sealed class PopulationEventPayloadFormatter : IEventPayloadFormatter
{
    public bool TryFormat(string kind, string payload, out string formatted)
    {
        formatted = "";

        if (string.Equals(kind, "Fire", StringComparison.OrdinalIgnoreCase))
            return TryFormatSingle(payload, "damage", "Damage", "Damage", out formatted);

        if (string.Equals(kind, "PopulationDelta", StringComparison.OrdinalIgnoreCase))
            return TryFormatSingle(payload, "delta", "Delta", "Delta", out formatted);

        if (string.Equals(kind, "FireScheduled", StringComparison.OrdinalIgnoreCase))
            return TryFormatScheduled(payload, out formatted);

        return false;
    }

    private static bool TryFormatSingle(string payload, string jsonName, string legacyName, string label, out string formatted)
    {
        formatted = "";
        if (string.IsNullOrWhiteSpace(payload)) return false;

        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Number && root.TryGetInt32(out var n))
            {
                formatted = $"{label}={n}";
                return true;
            }

            if (root.ValueKind == JsonValueKind.String && int.TryParse(root.GetString(), out var legacyValue))
            {
                formatted = $"{label}={legacyValue}";
                return true;
            }

            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty(jsonName, out var prop) || root.TryGetProperty(legacyName, out prop))
                {
                    if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var value))
                    {
                        formatted = $"{label}={value}";
                        return true;
                    }
                }
            }

            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryFormatScheduled(string payload, out string formatted)
    {
        formatted = "";
        if (string.IsNullOrWhiteSpace(payload)) return false;

        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
            {
                var hasAt = root.TryGetProperty("at", out var atEl) || root.TryGetProperty("At", out atEl);
                var hasDamage = root.TryGetProperty("damage", out var dmgEl) || root.TryGetProperty("Damage", out dmgEl);

                if (hasAt && hasDamage && atEl.TryGetInt64(out var at) && dmgEl.TryGetInt32(out var damage))
                {
                    formatted = $"At={at} Damage={damage}";
                    return true;
                }
            }

            if (root.ValueKind == JsonValueKind.String)
            {
                var legacy = root.GetString() ?? "";
                var parts = legacy.Split('|');
                if (parts.Length == 2 && long.TryParse(parts[0], out var at) && int.TryParse(parts[1], out var damage))
                {
                    formatted = $"At={at} Damage={damage}";
                    return true;
                }
            }

            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
