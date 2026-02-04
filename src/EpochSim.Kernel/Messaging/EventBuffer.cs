namespace EpochSim.Kernel.Messaging;

public sealed class EventBuffer : IEventBuffer
{
    private List<IEvent> _items = new();
    private List<IEvent> _drainBuffer = new();

    public void Emit(IEvent ev) => _items.Add(ev);

    public IReadOnlyList<IEvent> Drain()
    {
        if (_items.Count == 0) return Array.Empty<IEvent>();

        var drained = _items;
        _items = _drainBuffer;
        _drainBuffer = drained;
        _items.Clear();
        return drained;
    }
}
