using EpochSim.Execution.StateFingerprint;
using EpochSim.Kernel.Time;
using EpochSim.Serialization.State;

namespace EpochSim.Execution.Middleware;

public sealed class StateFingerprintMiddleware<TState>(
    TState state,
    IStateSerializer<TState> serializer,
    IStateFingerprintSink sink) : IExecutionMiddleware
{
    public void OnTickEnd(SimTime time)
    {
        var json = serializer.Serialize(state);
        var fp = Serialization.State.StateFingerprint.ComputeFromJson(json);
        sink.OnRecord(time.Tick, fp);
    }
}
