namespace EpochSim.Serialization.State;

public interface IStateSerializer<TState>
{
    string Serialize(TState state);
    TState Deserialize(string json);
}
