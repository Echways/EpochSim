using EpochSim.Kernel.Time;
using EpochSim.Kernel.Messaging;

namespace EpochSim.Kernel.Scheduling;

public interface IScheduler
{
    void Schedule(SimTime time, IEvent ev);
    bool TryDequeue(out ScheduledItem item);
    SimTime? PeekTime();
}