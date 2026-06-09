using System.Text;
using System.Text.Json;

namespace EpochSim.Serialization.State;

public static class CanonicalJson
{
    public static string Canonicalize(string json)
    {
        using var doc = JsonDocument.Parse(json);
        using var ms = new MemoryStream();
        using (var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = false }))
        {
            WriteCanonical(writer, doc.RootElement);
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }
                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                    WriteCanonical(writer, item);
                writer.WriteEndArray();
                break;

            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;

            case JsonValueKind.Number:
                // Normalize numbers so that equivalent values (e.g. 1.0 and 1, 2.50 and 2.5)
                // produce the same canonical form and therefore the same fingerprint.
                // Integer-valued numbers are written without a decimal point.
                // Non-integer doubles are written via WriteNumberValue which uses G17 round-trip format.
                // Limitation: numbers outside the double precision range (> 2^53) that are not
                // representable as int64/uint64 fall back to raw text; see Determinism.md.
                if (element.TryGetInt64(out var longVal))
                    writer.WriteNumberValue(longVal);
                else if (element.TryGetUInt64(out var ulongVal))
                    writer.WriteNumberValue(ulongVal);
                else if (element.TryGetDouble(out var doubleVal))
                    writer.WriteNumberValue(doubleVal);
                else
                    writer.WriteRawValue(element.GetRawText(), skipInputValidation: true);
                break;

            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;

            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;

            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;

            default:
                writer.WriteRawValue(element.GetRawText(), skipInputValidation: true);
                break;
        }
    }
}
