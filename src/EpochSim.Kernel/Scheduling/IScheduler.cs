using EpochSim.Kernel.Messaging;
using EpochSim.Kernel.Time;

namespace EpochSim.Kernel.Scheduling;

public interface IScheduler
{
    void ScheduleAt(SimTime time, IEvent ev);
    void ScheduleNextTick(IEvent ev);
    void ScheduleInTicks(long deltaTicks, IEvent ev);
}
