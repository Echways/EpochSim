using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace EpochSim.Serialization.State;

public static class StateDiff
{
    private const int MaxValueLength = 80;
    private const string Ellipsis = "…";

    private static readonly JsonSerializerOptions RenderOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static IReadOnlyList<StateChange> Compute(string beforeJson, string afterJson)
    {
        ArgumentNullException.ThrowIfNull(beforeJson);
        ArgumentNullException.ThrowIfNull(afterJson);

        var before = JsonNode.Parse(CanonicalJson.Canonicalize(beforeJson));
        var after = JsonNode.Parse(CanonicalJson.Canonicalize(afterJson));

        var changes = new List<StateChange>();
        Compare("$", before, after, changes);
        changes.Sort((left, right) => string.CompareOrdinal(left.Path, right.Path));
        return changes;
    }

    private static void Compare(string path, JsonNode? before, JsonNode? after, List<StateChange> changes)
    {
        switch (before, after)
        {
            case (JsonObject beforeObject, JsonObject afterObject):
                CompareObjects(path, beforeObject, afterObject, changes);
                break;

            case (JsonArray beforeArray, JsonArray afterArray):
                CompareArrays(path, beforeArray, afterArray, changes);
                break;

            default:
                var beforeText = Render(before);
                var afterText = Render(after);
                if (!string.Equals(beforeText, afterText, StringComparison.Ordinal))
                    changes.Add(new StateChange(path, StateChangeKind.Changed, Truncate(beforeText), Truncate(afterText)));
                break;
        }
    }

    private static void CompareObjects(string path, JsonObject before, JsonObject after, List<StateChange> changes)
    {
        foreach (var (key, beforeValue) in before)
        {
            var childPath = AppendKey(path, key);
            if (after.TryGetPropertyValue(key, out var afterValue))
                Compare(childPath, beforeValue, afterValue, changes);
            else
                changes.Add(new StateChange(childPath, StateChangeKind.Removed, Truncate(Render(beforeValue)), null));
        }

        foreach (var (key, afterValue) in after)
        {
            if (!before.ContainsKey(key))
                changes.Add(new StateChange(AppendKey(path, key), StateChangeKind.Added, null, Truncate(Render(afterValue))));
        }
    }

    private static void CompareArrays(string path, JsonArray before, JsonArray after, List<StateChange> changes)
    {
        var shared = Math.Min(before.Count, after.Count);

        for (var i = 0; i < shared; i++)
            Compare(AppendIndex(path, i), before[i], after[i], changes);

        for (var i = shared; i < before.Count; i++)
            changes.Add(new StateChange(AppendIndex(path, i), StateChangeKind.Removed, Truncate(Render(before[i])), null));

        for (var i = shared; i < after.Count; i++)
            changes.Add(new StateChange(AppendIndex(path, i), StateChangeKind.Added, null, Truncate(Render(after[i]))));
    }

    private static string AppendIndex(string path, int index)
        => $"{path}[{index}]";

    private static string AppendKey(string path, string key)
        => IsSimpleKey(key)
            ? $"{path}.{key}"
            : $"{path}[\"{key.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"]";

    private static bool IsSimpleKey(string key)
        => key.Length > 0 && key.All(static c => char.IsLetterOrDigit(c) || c == '_');

    private static string Render(JsonNode? node)
        => node is null ? "null" : node.ToJsonString(RenderOptions);

    private static string Truncate(string value)
    {
        if (value.Length <= MaxValueLength)
            return value;

        var keep = MaxValueLength - Ellipsis.Length;
        if (char.IsHighSurrogate(value[keep - 1]))
            keep--;

        return string.Concat(value.AsSpan(0, keep), Ellipsis);
    }
}
