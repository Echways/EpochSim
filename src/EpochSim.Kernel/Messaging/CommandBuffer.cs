namespace EpochSim.Kernel.Messaging;

public interface ICommandBuffer
{
    void Enqueue(ICommand command);
    IReadOnlyList<ICommand> Drain();
}

public sealed class CommandBuffer : ICommandBuffer
{
    private List<ICommand> _items = new();
    private List<ICommand> _drainBuffer = new();

    public void Enqueue(ICommand command) => _items.Add(command);

    public IReadOnlyList<ICommand> Drain()
    {
        if (_items.Count == 0) return Array.Empty<ICommand>();

        var drained = _items;
        _items = _drainBuffer;
        _drainBuffer = drained;
        _items.Clear();
        return drained;
    }
}
