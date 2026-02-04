using System.Threading;
using EpochSim.Kernel.Time;
using EpochSim.Serialization.EventLog;
using EpochSim.Serialization.Snapshots;
using EpochSim.Serialization.State;

namespace EpochSim.Execution.Snapshots;

public static class SnapshotRunner
{
    public static TState LoadBestAndReplayTo<TState>(
        SimulationEngine<TState> engine,
        string snapshotsDir,
        string eventsPath,
        IStateSerializer<TState> serializer,
        IEventCodecV2 codec,
        ulong seed,
        long endTick,
        Func<TState> newState,
        RunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var snapshotPath = SnapshotLocator.FindBestSnapshot(snapshotsDir, endTick);

        TState state;
        long startTick;

        if (snapshotPath is null)
        {
            state = newState();
            startTick = 0;
        }
        else
        {
            var snap = SnapshotReader.Read(snapshotPath);
            state = serializer.Deserialize(snap.StateJson);
            startTick = snap.Tick + 1;
        }

        var entries = startTick == 0
            ? EventLogReader.ReadStream(eventsPath)
            : EventLogReader.ReadStream(eventsPath).Where(e => e.Tick > startTick - 1);

        engine.ReplayFromLogStream(
            state,
            seed,
            new SimTime(startTick),
            new SimTime(endTick),
            entries,
            codec,
            options: options,
            cancellationToken: cancellationToken);
        return state;
    }
}
