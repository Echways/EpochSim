using EpochSim.Kernel.Messaging;

namespace EpochSim.Kernel.Systems;

public interface ISystem<TState>
{
    string Name => GetType().Name;
    void Tick(TickContext<TState> ctx);
    void Handle(EventContext<TState> ctx, IEvent ev) { }
}
