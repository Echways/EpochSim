using System.Text;
using System.Text.Json;

namespace EpochSim.Serialization.State;

public static class CanonicalJson
{
    public static string Canonicalize(string json)
    {
        using var doc = JsonDocument.Parse(json);
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = false }))
        {
            WriteCanonical(w, doc.RootElement);
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static void WriteCanonical(Utf8JsonWriter w, JsonElement e)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Object:
                w.WriteStartObject();
                foreach (var p in e.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    w.WritePropertyName(p.Name);
                    WriteCanonical(w, p.Value);
                }
                w.WriteEndObject();
                break;

            case JsonValueKind.Array:
                w.WriteStartArray();
                foreach (var item in e.EnumerateArray())
                    WriteCanonical(w, item);
                w.WriteEndArray();
                break;

            case JsonValueKind.String:
                w.WriteStringValue(e.GetString());
                break;

            case JsonValueKind.Number:
                w.WriteRawValue(e.GetRawText(), skipInputValidation: true);
                break;

            case JsonValueKind.True:
                w.WriteBooleanValue(true);
                break;

            case JsonValueKind.False:
                w.WriteBooleanValue(false);
                break;

            case JsonValueKind.Null:
                w.WriteNullValue();
                break;

            default:
                w.WriteRawValue(e.GetRawText(), skipInputValidation: true);
                break;
        }
    }
}