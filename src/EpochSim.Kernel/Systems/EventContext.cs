using EpochSim.Kernel.Time;
using EpochSim.Kernel.Determinism;
using EpochSim.Kernel.Scheduling;
using EpochSim.Kernel.Messaging;

namespace EpochSim.Kernel.Systems;

public sealed class EventContext<TState>(
    SimTime time,
    TState state,
    IScheduler scheduler,
    ICommandBuffer commands,
    IEventEmitter events,
    IRng rng)
{
    public SimTime Time { get; } = time;
    public TState State { get; } = state;
    public IScheduler Scheduler { get; } = scheduler;
    public ICommandBuffer Commands { get; } = commands;
    public IEventEmitter Events { get; } = events;
    public IRng Rng { get; } = rng;
}
