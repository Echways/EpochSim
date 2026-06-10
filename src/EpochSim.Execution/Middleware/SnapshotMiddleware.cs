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
        if (time.Tick == 0) return; // tick 0 has no event-log entry to replay against
        if (time.Tick % intervalTicks != 0) return;

        Directory.CreateDirectory(directory);
        var snapshotPath = Path.Combine(directory, $"snapshot-{time.Tick}.json");
        SnapshotWriter.Write(snapshotPath, tick: time.Tick, stateJson: serializer.Serialize(state));
    }
}
