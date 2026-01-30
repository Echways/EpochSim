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
        IEventCodec codec,
        ulong seed,
        long endTick,
        Func<TState> newState)
    {
        var snapPath = SnapshotLocator.FindBestSnapshot(snapshotsDir, endTick);

        TState state;
        long startTick;

        if (snapPath is null)
        {
            state = newState();
            startTick = 0;
        }
        else
        {
            var snap = SnapshotReader.Read(snapPath);
            state = serializer.Deserialize(snap.StateJson);
            startTick = snap.Tick + 1;
        }

        var entries = startTick == 0
            ? EventLogReader.ReadAll(eventsPath)
            : EventLogReader.ReadAfterTick(eventsPath, startTick - 1);

        var index = new EventLogIndex(entries);

        engine.ReplayFromLog(state, seed, new SimTime(startTick), new SimTime(endTick), index, codec);
        return state;
    }
}