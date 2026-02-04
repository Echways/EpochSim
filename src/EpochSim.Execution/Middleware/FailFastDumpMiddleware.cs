using EpochSim.Kernel.Validation;
using EpochSim.Serialization.EventLog;
using EpochSim.Serialization.Snapshots;
using EpochSim.Serialization.State;

namespace EpochSim.Execution.Middleware;

public sealed class FailFastDumpMiddleware<TState>(
    TState state,
    Func<long> currentTickProvider,
    Func<IReadOnlyList<EventLogEntryV2>> eventLogProvider,
    IStateSerializer<TState> serializer,
    string dumpDirectory)
{
    public void DumpOnViolation(InvariantViolationException ex)
    {
        Directory.CreateDirectory(dumpDirectory);

        var tick = currentTickProvider();
        if (tick < 0) tick = ex.Time.Tick;

        var snapshotPath = Path.Combine(dumpDirectory, $"violation-snapshot-{tick}.json");
        SnapshotWriter.Write(snapshotPath, tick, serializer.Serialize(state));

        var eventsLogPath = Path.Combine(dumpDirectory, $"violation-events-{tick}.jsonl");
        using (var writer = new EventLogWriter(eventsLogPath))
        {
            var entries = eventLogProvider();
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                writer.Write(entry.Tick, entry.Kind, entry.PayloadJson);
            }
        }

        var metaPath = Path.Combine(dumpDirectory, $"violation-meta-{tick}.txt");
        File.WriteAllText(metaPath, $"{ex.InvariantName}\n{ex.Time.Tick}\n{ex.Detail}\n");
    }
}
