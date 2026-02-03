namespace EpochSim.Kernel.Messaging;

public sealed class CommandRouter<TState>
{
    private readonly Dictionary<Type, ICommandHandlerWrapper> _handlers = new();

    public void Register<TCommand>(ICommandHandler<TState, TCommand> handler)
        where TCommand : ICommand
        => _handlers[typeof(TCommand)] = new CommandHandlerWrapper<TCommand>(handler);

    public void DispatchAll(
        TState state,
        IReadOnlyList<ICommand> commands,
        IEventBuffer events,
        Action<string, ICommand>? before = null,
        Action<string, ICommand>? after = null)
    {
        foreach (var command in commands)
        {
            if (!_handlers.TryGetValue(command.GetType(), out var handler))
                throw new InvalidOperationException($"No handler registered for command {command.GetType().Name}");

            before?.Invoke(handler.Name, command);
            handler.Handle(state, command, events);
            after?.Invoke(handler.Name, command);
        }
    }

    private interface ICommandHandlerWrapper
    {
        string Name { get; }
        void Handle(TState state, ICommand command, IEventBuffer events);
    }

    private sealed class CommandHandlerWrapper<TCommand>(ICommandHandler<TState, TCommand> handler) : ICommandHandlerWrapper
        where TCommand : ICommand
    {
        public string Name => handler.GetType().Name;

        public void Handle(TState state, ICommand command, IEventBuffer events)
        {
            if (command is not TCommand typed)
                throw new InvalidOperationException($"Handler {Name} expected {typeof(TCommand).Name} but got {command.GetType().Name}");

            handler.Handle(state, typed, events);
        }
    }
}
