using System.Text.Json;

namespace EpochSim.Serialization.EventLog;

public static class EventLogLine
{
    public static EventLogEntryV2 ReadEntry(string line)
    {
        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;

        if (!root.TryGetProperty("t", out var t))
            throw new FormatException("Missing field t");
        if (!root.TryGetProperty("kind", out var k))
            throw new FormatException("Missing field kind");
        if (!root.TryGetProperty("payload", out var p))
            throw new FormatException("Missing field payload");

        if (k.ValueKind != JsonValueKind.String)
            throw new FormatException("Invalid kind (expected string)");

        var tick = t.GetInt64();
        var kind = k.GetString() ?? "";
        var payloadJson = p.GetRawText();

        return new EventLogEntryV2(tick, kind, payloadJson);
    }
}
