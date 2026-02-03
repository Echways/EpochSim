namespace EpochSim.Kernel.Messaging;

public interface IEventEmitter
{
    void Emit(IEvent ev);
}
