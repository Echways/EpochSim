using EpochSim.Kernel.Messaging;
using EpochSim.Kernel.Time;
using EpochSim.Serialization.EventLog;

namespace EpochSim.Execution.Middleware;

public sealed class InMemoryEventLogMiddleware(IEventCodecV2 codec) : IExecutionMiddleware
{
    private readonly List<EventLogEntryV2> _entries = [];
    public IReadOnlyList<EventLogEntryV2> Entries => _entries;

    public long CurrentTick { get; private set; }
    public int EventsThisTick { get; private set; }

    public void OnTickStart(SimTime time)
    {
        CurrentTick = time.Tick;
        EventsThisTick = 0;
    }

    public void OnEventDispatched(SimTime time, IEvent ev)
    {
        if (!codec.TryEncode(ev, out var kind, out var payloadJson))
            throw new InvalidOperationException($"No codec for event {ev.GetType().Name}");

        _entries.Add(new EventLogEntryV2(time.Tick, kind, payloadJson));
        EventsThisTick++;
    }
}
