using EpochSim.Kernel.Messaging;
using EpochSim.Kernel.Time;
using EpochSim.Serialization.EventLog;

namespace EpochSim.Execution.Middleware;

public sealed class InMemoryEventLogMiddleware(IEventCodec codec) : IExecutionMiddleware
{
    private readonly List<EventLogEntry> _entries = [];
    public IReadOnlyList<EventLogEntry> Entries => _entries;

    public long CurrentTick { get; private set; }
    public int EventsThisTick { get; private set; }

    public void OnTickStart(SimTime time)
    {
        CurrentTick = time.Tick;
        EventsThisTick = 0;
    }

    public void OnEventDispatched(SimTime time, IEvent ev)
    {
        if (!codec.TryEncode(ev, out var kind, out var payload))
            throw new InvalidOperationException($"No codec for event {ev.GetType().Name}");

        _entries.Add(new EventLogEntry(time.Tick, kind, payload));
        EventsThisTick++;
    }
}
