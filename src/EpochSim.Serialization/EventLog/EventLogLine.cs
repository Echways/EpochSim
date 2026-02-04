using System.Text.Json;

namespace EpochSim.Serialization.EventLog;

public static class EventLogLine
{
    public static EventLogEntryV2 ReadEntry(string line)
    {
        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;

        if (!root.TryGetProperty("t", out var tickProp))
            throw new FormatException("Missing field t");
        if (!root.TryGetProperty("kind", out var kindProp))
            throw new FormatException("Missing field kind");
        if (!root.TryGetProperty("payload", out var payloadProp))
            throw new FormatException("Missing field payload");

        if (kindProp.ValueKind != JsonValueKind.String)
            throw new FormatException("Invalid kind (expected string)");

        var tick = tickProp.GetInt64();
        var kind = kindProp.GetString() ?? "";
        var payloadJson = payloadProp.GetRawText();

        return new EventLogEntryV2(tick, kind, payloadJson);
    }
}
