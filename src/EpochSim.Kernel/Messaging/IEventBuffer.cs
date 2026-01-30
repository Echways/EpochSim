namespace EpochSim.Kernel.Messaging;

public interface IEventBuffer
{
    void Emit(IEvent ev);
    IReadOnlyList<IEvent> Drain();
}