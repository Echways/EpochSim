using System.Threading;
using EpochSim.Kernel.Determinism;
using EpochSim.Kernel.Messaging;
using EpochSim.Kernel.Scheduling;
using EpochSim.Kernel.Time;

namespace EpochSim.Kernel.Systems;

public sealed class TickContext<TState>(
    SimTime time,
    TState state,
    IScheduler scheduler,
    ICommandBuffer commands,
    IRng rng,
    CancellationToken cancellation)
{
    public SimTime Time { get; } = time;
    public TState State { get; } = state;
    public IScheduler Scheduler { get; } = scheduler;
    public ICommandBuffer Commands { get; } = commands;
    public IRng Rng { get; } = rng;
    public CancellationToken Cancellation { get; } = cancellation;
}
