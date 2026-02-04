using System.Threading;
using EpochSim.Kernel.Determinism;
using EpochSim.Kernel.Messaging;
using EpochSim.Kernel.Scheduling;
using EpochSim.Kernel.Time;

namespace EpochSim.Kernel.Systems;

public sealed class EventContext<TState>(
    SimTime time,
    TState state,
    IScheduler scheduler,
    ICommandBuffer commands,
    IEventEmitter events,
    IRng rng,
    CancellationToken cancellation)
{
    public SimTime Time { get; } = time;
    public TState State { get; } = state;
    public IScheduler Scheduler { get; } = scheduler;
    public ICommandBuffer Commands { get; } = commands;
    public IEventEmitter Events { get; } = events;
    public IRng Rng { get; } = rng;
    public CancellationToken Cancellation { get; } = cancellation;
}
