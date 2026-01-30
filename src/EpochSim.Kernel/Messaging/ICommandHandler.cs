namespace EpochSim.Kernel.Messaging;

public interface ICommandHandler<TState>
{
    Type CommandType { get; }
    void Handle(TState state, ICommand command, IEventBuffer events);
}