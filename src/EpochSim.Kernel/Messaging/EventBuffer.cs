namespace EpochSim.Kernel.Messaging;

public sealed class EventBuffer : IEventBuffer
{
    private readonly List<IEvent> _items = new();

    public void Emit(IEvent ev) => _items.Add(ev);

    public IReadOnlyList<IEvent> Drain()
    {
        if (_items.Count == 0) return Array.Empty<IEvent>();
        var copy = _items.ToArray();
        _items.Clear();
        return copy;
    }
}
