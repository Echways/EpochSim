namespace EpochSim.Kernel.Messaging;

public interface ICommandBuffer
{
    void Enqueue(ICommand command);
    IReadOnlyList<ICommand> Drain();
}

public sealed class CommandBuffer : ICommandBuffer
{
    private readonly List<ICommand> _items = new();

    public void Enqueue(ICommand command) => _items.Add(command);

    public IReadOnlyList<ICommand> Drain()
    {
        if (_items.Count == 0) return Array.Empty<ICommand>();
        var copy = _items.ToArray();
        _items.Clear();
        return copy;
    }
}
