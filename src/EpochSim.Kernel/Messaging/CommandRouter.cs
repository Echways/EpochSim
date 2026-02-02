namespace EpochSim.Kernel.Messaging;

public sealed class CommandRouter<TState>
{
    private readonly Dictionary<Type, ICommandHandler<TState>> _handlers = new();

    public void Register(ICommandHandler<TState> handler)
        => _handlers[handler.CommandType] = handler;

    public void DispatchAll(TState state, IReadOnlyList<ICommand> commands, IEventBuffer events)
    {
        foreach (var cmd in commands)
        {
            if (!_handlers.TryGetValue(cmd.GetType(), out var handler))
                throw new InvalidOperationException($"No handler registered for command {cmd.GetType().Name}");

            handler.Handle(state, cmd, events);
        }
    }
}
