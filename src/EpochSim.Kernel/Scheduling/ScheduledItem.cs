using EpochSim.Kernel.Messaging;
using EpochSim.Kernel.Time;

namespace EpochSim.Kernel.Scheduling;

public readonly record struct ScheduledItem(SimTime Time, long Seq, IEvent Event);
