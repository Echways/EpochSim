using EpochSim.Serialization.State;

namespace EpochSim.Fuzzing;

internal sealed class ProjectedStateSerializer<TState>(Func<TState, object> projection) : IStateSerializer<TState>
{
    private readonly JsonStateSerializer<object> _json = new();

    public string Serialize(TState state)
        => _json.Serialize(projection(state));

    public TState Deserialize(string json)
        => throw new NotSupportedException("A projected state cannot be deserialized.");
}
