using EpochSim.Execution.Validation;
using EpochSim.Serialization.EventLog;
using EpochSim.Serialization.Snapshots;
using EpochSim.Serialization.State;

namespace EpochSim.Execution.Middleware;

public sealed class FailFastDumpMiddleware<TState>(
    TState state,
    Func<long> currentTickProvider,
    Func<IReadOnlyList<EventLogEntry>> eventLogProvider,
    IStateSerializer<TState> serializer,
    string dumpDirectory)
{
    public void DumpOnViolation(InvariantViolationException ex)
    {
        Directory.CreateDirectory(dumpDirectory);

        var tick = currentTickProvider();
        if (tick < 0) tick = ex.Time.Tick;

        var snapPath = Path.Combine(dumpDirectory, $"violation-snapshot-{tick}.json");
        SnapshotWriter.Write(snapPath, tick, serializer.Serialize(state));

        var eventsPath = Path.Combine(dumpDirectory, $"violation-events-{tick}.jsonl");
        using (var writer = new EventLogWriter(eventsPath))
        {
            var entries = eventLogProvider();
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                writer.Write(e.Tick, e.Kind, e.Payload);
            }
        }

        var metaPath = Path.Combine(dumpDirectory, $"violation-meta-{tick}.txt");
        File.WriteAllText(metaPath, $"{ex.InvariantName}\n{ex.Time.Tick}\n{ex.Detail}\n");
    }
}
