using EpochSim.Execution.StateFingerprint;
using EpochSim.Kernel.Time;
using EpochSim.Serialization.State;

namespace EpochSim.Execution.Middleware;

public sealed class StateFingerprintMiddleware<TState>(
    TState state,
    IStateSerializer<TState> serializer,
    IStateFingerprintSink sink,
    long intervalTicks = 1) : IExecutionMiddleware
{
    public void OnTickEnd(SimTime time)
    {
        if (intervalTicks <= 0) return;
        if (time.Tick % intervalTicks != 0) return;

        var json = serializer.Serialize(state);
        var fp = EpochSim.Serialization.State.StateFingerprint.ComputeFromJson(json);
        sink.OnRecord(time.Tick, fp);
    }
}
