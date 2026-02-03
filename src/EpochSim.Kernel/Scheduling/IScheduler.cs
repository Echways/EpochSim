using EpochSim.Kernel.Time;
using EpochSim.Kernel.Messaging;

namespace EpochSim.Kernel.Scheduling;

public interface IScheduler
{
    void ScheduleAt(SimTime time, IEvent ev);
    void ScheduleNextTick(IEvent ev);
    void ScheduleInTicks(long deltaTicks, IEvent ev);
}
