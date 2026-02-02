using EpochSim.Kernel.Time;
using EpochSim.Serialization.Snapshots;
using EpochSim.Serialization.State;

namespace EpochSim.Execution.Middleware;

public sealed class SnapshotMiddleware<TState>(
    TState state,
    long intervalTicks,
    string directory,
    IStateSerializer<TState> serializer) : IExecutionMiddleware
{
    public void OnTickEnd(SimTime time)
    {
        if (intervalTicks <= 0) return;
        if (time.Tick == 0) return;
        if (time.Tick % intervalTicks != 0) return;

        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"snapshot-{time.Tick}.json");
        SnapshotWriter.Write(path, tick: time.Tick, stateJson: serializer.Serialize(state));
    }
}
