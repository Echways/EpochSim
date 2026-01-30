using EpochSim.Kernel.Messaging;

namespace EpochSim.Kernel.Systems;

public interface ISystem<TState>
{
    string Name { get; }

    void Tick(TickContext<TState> ctx);
    void Handle(EventContext<TState> ctx, IEvent ev);
}