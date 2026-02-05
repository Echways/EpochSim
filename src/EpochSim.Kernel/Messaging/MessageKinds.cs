using System.Collections.Concurrent;
using System.Reflection;

namespace EpochSim.Kernel.Messaging;

public static class MessageKinds
{
    private static readonly ConcurrentDictionary<Type, string> Cache = new();

    public static string GetKind(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return Cache.GetOrAdd(type, ResolveKind);
    }

    private static string ResolveKind(Type type)
    {
        var attribute = type.GetCustomAttribute<MessageKindAttribute>(inherit: false);
        if (attribute is null)
            return type.Name;

        if (string.IsNullOrWhiteSpace(attribute.Kind))
            throw new InvalidOperationException($"Message kind for {type.FullName} cannot be empty.");

        return attribute.Kind;
    }

    internal static void ResetForTests() => Cache.Clear();
    internal static int CachedTypeCountForTests => Cache.Count;
}
