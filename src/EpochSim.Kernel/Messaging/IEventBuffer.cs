namespace EpochSim.Kernel.Messaging;

public interface IEventBuffer : IEventEmitter
{
    IReadOnlyList<IEvent> Drain();
}
