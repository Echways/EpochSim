using System.Text.Json;
using EpochSim.Execution.RunArtifacts;
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
    IEventCodecV2? codec,
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

        string kind;
        string payloadJson;

        if (codec is not null)
        {
            if (!codec.TryEncode(ev, out kind, out payloadJson))
            {
                throw new InvalidOperationException(
                    $"Failure artifact encoding failed for event '{ev.GetType().FullName}'. " +
                    "Why: the configured codec does not know this event type. " +
                    "Fix: register the event in your codec builder, for example: " +
                    "new JsonEventCodecBuilder().Register<YourEvent>().Build().");
            }
        }
        else
        {
            kind = ev.Kind;

            try
            {
                payloadJson = JsonSerializer.Serialize(ev, ev.GetType(), JsonOptions);
            }
            catch (Exception ex) when (ex is JsonException || ex is NotSupportedException)
            {
                throw new InvalidOperationException(
                    $"Failure artifact encoding failed for event '{ev.GetType().FullName}'. " +
                    "Why: no codec was configured and runtime JSON serialization could not encode the event. " +
                    "Fix: provide an event codec explicitly, for example: " +
                    "Epoch.RecommendedRun(state, codec, serializer).", ex);
            }
        }

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
            ? Path.Combine(info.ArtifactDir, RunPaths.FailureSnapshotFileName)
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

        var reportPath = Path.Combine(info.ArtifactDir, RunPaths.FailureReportFileName);
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
