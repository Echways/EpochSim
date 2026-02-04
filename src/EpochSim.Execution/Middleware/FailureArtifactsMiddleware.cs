using System.Text.Json;
using EpochSim.Kernel.Messaging;
using EpochSim.Kernel.Time;
using EpochSim.Kernel.Validation;
using EpochSim.Serialization.EventLog;
using EpochSim.Serialization.Snapshots;
using EpochSim.Serialization.State;

namespace EpochSim.Execution.Middleware;

public sealed class FailureArtifactsMiddleware<TState>(
    TState state,
    IStateSerializer<TState> serializer,
    IEventCodecV2 codec,
    bool snapshotEnabled,
    int tailSize = 200) : IExecutionMiddleware
{
    private readonly int _tailSize = Math.Max(0, tailSize);
    private readonly Queue<EventLogEntryV2> _tail = new(Math.Max(1, tailSize));
    private long? _currentTick;

    public void OnRunStart(RunInfo info)
    {
        _tail.Clear();
        _currentTick = null;
    }

    public void OnTickStart(SimTime time)
        => _currentTick = time.Tick;

    public void OnEventDispatched(SimTime time, IEvent ev)
    {
        if (_tailSize == 0)
            return;

        if (!codec.TryEncode(ev, out var kind, out var payloadJson))
            throw new InvalidOperationException($"No codec for event {ev.GetType().Name}");

        var entry = new EventLogEntryV2(time.Tick, kind, payloadJson);
        _tail.Enqueue(entry);
        while (_tail.Count > _tailSize)
            _tail.Dequeue();
    }

    public void OnRunFailed(RunInfo info, Exception exception)
    {
        if (exception is OperationCanceledException)
            return;

        if (string.IsNullOrWhiteSpace(info.ArtifactDir))
            return;

        Directory.CreateDirectory(info.ArtifactDir);

        var failureTick = ResolveFailureTick(exception);
        var snapshotPath = snapshotEnabled && failureTick.HasValue
            ? Path.Combine(info.ArtifactDir, "failure-snapshot.json")
            : null;

        if (snapshotPath is not null && failureTick is long tick)
        {
            var stateJson = serializer.Serialize(state);
            SnapshotWriter.Write(snapshotPath, tick, stateJson);
        }

        var report = new FailureReport(
            RunId: info.RunId,
            Mode: info.Mode.ToString(),
            Tick: failureTick,
            ExceptionType: exception.GetType().FullName ?? exception.GetType().Name,
            ExceptionMessage: exception.Message,
            ExceptionStack: exception.StackTrace ?? "",
            OccurredUtc: DateTime.UtcNow,
            LastEvents: _tail.ToArray(),
            SnapshotPath: snapshotPath);

        var reportPath = Path.Combine(info.ArtifactDir, "failure-report.json");
        var json = JsonSerializer.Serialize(report, JsonOptions);
        File.WriteAllText(reportPath, json);
    }

    private long? ResolveFailureTick(Exception exception)
    {
        if (exception is InvariantViolationException inv)
            return inv.Time.Tick;

        return _currentTick;
    }

    private sealed record FailureReport(
        string? RunId,
        string Mode,
        long? Tick,
        string ExceptionType,
        string ExceptionMessage,
        string ExceptionStack,
        DateTime OccurredUtc,
        IReadOnlyList<EventLogEntryV2> LastEvents,
        string? SnapshotPath);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };
}
