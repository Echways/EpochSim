using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using EpochSim.Kernel.Messaging;

namespace EpochSim.Serialization.EventLog;

public sealed class JsonEventCodecBuilder
{
    private readonly Dictionary<Type, Registration> _byType = new();
    private readonly Dictionary<string, Registration> _byKind = new(StringComparer.Ordinal);

    private JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        PropertyNameCaseInsensitive = true
    };

    private bool _strictUnknownKinds;

    public JsonEventCodecBuilder Register<TEvent>(string? kind = null)
        where TEvent : class, IEvent
        => Register(typeof(TEvent), kind);

    public JsonEventCodecBuilder Register(Type eventType, string? kind = null)
    {
        ArgumentNullException.ThrowIfNull(eventType);

        if (!eventType.IsClass || eventType.IsAbstract || eventType.IsGenericTypeDefinition)
            throw new ArgumentException(
                $"Event type '{eventType.FullName}' must be a non-abstract concrete class.",
                nameof(eventType));

        if (!typeof(IEvent).IsAssignableFrom(eventType))
            throw new ArgumentException(
                $"Type '{eventType.FullName}' must implement {nameof(IEvent)}.",
                nameof(eventType));

        if (_byType.ContainsKey(eventType))
            throw new InvalidOperationException($"Event type '{eventType.FullName}' is already registered.");

        var resolvedKind = kind ?? MessageKinds.GetKind(eventType);
        if (string.IsNullOrWhiteSpace(resolvedKind))
            throw new InvalidOperationException($"Event kind for '{eventType.FullName}' cannot be empty.");

        if (_byKind.TryGetValue(resolvedKind, out var existing))
            throw new InvalidOperationException(
                $"Event kind '{resolvedKind}' is already registered by '{existing.EventType.FullName}'.");

        var registration = new Registration(resolvedKind, eventType);
        _byType[eventType] = registration;
        _byKind[resolvedKind] = registration;

        return this;
    }

    public JsonEventCodecBuilder RegisterFromAssembly(Assembly assembly, Func<Type, bool>? filter = null)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var candidates = assembly.DefinedTypes
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IEvent).IsAssignableFrom(type.AsType()))
            .Select(type => type.AsType())
            .OrderBy(type => type.FullName, StringComparer.Ordinal);

        foreach (var candidate in candidates)
        {
            if (filter is not null && !filter(candidate))
                continue;

            Register(candidate);
        }

        return this;
    }

    public JsonEventCodecBuilder RegisterFrom<TMarker>()
        => RegisterFromAssembly(typeof(TMarker).Assembly);

    public JsonEventCodecBuilder WithStrictUnknownKinds(bool strict = true)
    {
        _strictUnknownKinds = strict;
        return this;
    }

    public JsonEventCodecBuilder WithJsonOptions(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _jsonOptions = new JsonSerializerOptions(options);
        return this;
    }

    public IEventCodecV2 Build()
    {
        var byType = new Dictionary<Type, Registration>(_byType);
        var byKind = new Dictionary<string, Registration>(_byKind, StringComparer.Ordinal);
        return new JsonEventCodec(byType, byKind, _jsonOptions, _strictUnknownKinds);
    }

    private readonly record struct Registration(string Kind, Type EventType);

    private sealed class JsonEventCodec(
        IReadOnlyDictionary<Type, Registration> byType,
        IReadOnlyDictionary<string, Registration> byKind,
        JsonSerializerOptions jsonOptions,
        bool strictUnknownKinds) : IEventCodecV2
    {
        public bool TryEncode(IEvent ev, out string kind, out string payloadJson)
        {
            if (!byType.TryGetValue(ev.GetType(), out var registration))
            {
                kind = "";
                payloadJson = "";
                return false;
            }

            kind = registration.Kind;

            var utf8 = JsonSerializer.SerializeToUtf8Bytes(ev, registration.EventType, jsonOptions);
            ReadOnlySpan<byte> payloadSpan = utf8;
            payloadJson = Encoding.UTF8.GetString(payloadSpan);
            return true;
        }

        public bool TryDecode(string kind, string payloadJson, out IEvent ev)
        {
            if (!byKind.TryGetValue(kind, out var registration))
            {
                if (strictUnknownKinds)
                    throw new InvalidOperationException($"Unknown event kind '{kind}'.");

                ev = default!;
                return false;
            }

            try
            {
                var parsed = JsonSerializer.Deserialize(payloadJson, registration.EventType, jsonOptions);
                if (parsed is IEvent typed)
                {
                    ev = typed;
                    return true;
                }

                if (strictUnknownKinds)
                    throw new InvalidOperationException($"Decoded payload for kind '{kind}' was null.");

                ev = default!;
                return false;
            }
            catch (Exception ex) when (ex is JsonException || ex is NotSupportedException)
            {
                if (strictUnknownKinds)
                    throw new InvalidOperationException($"Failed to decode payload for kind '{kind}'.", ex);

                ev = default!;
                return false;
            }
        }
    }
}
