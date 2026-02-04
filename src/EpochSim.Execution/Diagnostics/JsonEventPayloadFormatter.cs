using System.Text;
using System.Text.Json;

namespace EpochSim.Execution.Diagnostics;

public sealed class JsonEventPayloadFormatter(int maxPairs = 16) : IEventPayloadFormatter
{
    public bool TryFormat(string kind, string payload, out string formatted)
    {
        payload ??= "";

        if (payload.Length == 0)
        {
            formatted = "";
            return true;
        }

        try
        {
            using var doc = JsonDocument.Parse(payload);
            formatted = Format(doc.RootElement);
            return true;
        }
        catch (JsonException)
        {
            formatted = payload;
            return true;
        }
    }

    private string Format(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => FormatObject(element),
            JsonValueKind.Array => $"[{element.GetArrayLength()}]",
            JsonValueKind.String => Quote(element.GetString() ?? ""),
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "null",
            _ => element.GetRawText()
        };
    }

    private string FormatObject(JsonElement obj)
    {
        var properties = obj.EnumerateObject()
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .Take(Math.Max(1, maxPairs))
            .ToArray();

        var builder = new StringBuilder();
        builder.Append('{');

        for (var i = 0; i < properties.Length; i++)
        {
            if (i > 0) builder.Append(", ");
            builder.Append(properties[i].Name);
            builder.Append('=');
            builder.Append(Format(properties[i].Value));
        }

        if (obj.EnumerateObject().Skip(properties.Length).Any())
            builder.Append(", ...");

        builder.Append('}');
        return builder.ToString();
    }

    private static string Quote(string s)
    {
        if (s.Length == 0) return "\"\"";
        if (s.Length > 120) s = s.Substring(0, 120) + "...";
        return "\"" + s.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }
}
