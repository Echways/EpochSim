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
        catch
        {
            formatted = payload;
            return true;
        }
    }

    private string Format(JsonElement e)
    {
        return e.ValueKind switch
        {
            JsonValueKind.Object => FormatObject(e),
            JsonValueKind.Array => $"[{e.GetArrayLength()}]",
            JsonValueKind.String => Quote(e.GetString() ?? ""),
            JsonValueKind.Number => e.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "null",
            _ => e.GetRawText()
        };
    }

    private string FormatObject(JsonElement obj)
    {
        var props = obj.EnumerateObject()
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .Take(Math.Max(1, maxPairs))
            .ToArray();

        var sb = new StringBuilder();
        sb.Append('{');

        for (var i = 0; i < props.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(props[i].Name);
            sb.Append('=');
            sb.Append(Format(props[i].Value));
        }

        if (obj.EnumerateObject().Skip(props.Length).Any())
            sb.Append(", ...");

        sb.Append('}');
        return sb.ToString();
    }

    private static string Quote(string s)
    {
        if (s.Length == 0) return "\"\"";
        if (s.Length > 120) s = s.Substring(0, 120) + "...";
        return "\"" + s.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }
}
