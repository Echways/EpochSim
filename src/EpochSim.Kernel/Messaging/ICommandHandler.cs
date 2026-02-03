namespace EpochSim.Kernel.Messaging;

public interface ICommandHandler<TState, in TCommand> where TCommand : ICommand
{
    void Handle(TState state, TCommand command, IEventBuffer events);
}
