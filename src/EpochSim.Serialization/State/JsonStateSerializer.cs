using System.Text.Json;

namespace EpochSim.Serialization.State;

public sealed class JsonStateSerializer<TState> : IStateSerializer<TState>
{
    private readonly JsonSerializerOptions _options;

    public JsonStateSerializer(JsonSerializerOptions? options = null)
    {
        _options = options ?? new JsonSerializerOptions
        {
            WriteIndented = false
        };
    }

    public string Serialize(TState state)
        => JsonSerializer.Serialize(state, _options);

    public TState Deserialize(string json)
        => JsonSerializer.Deserialize<TState>(json, _options) ?? throw new InvalidOperationException("Failed to deserialize state");
}
