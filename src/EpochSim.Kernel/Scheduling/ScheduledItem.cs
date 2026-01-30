using EpochSim.Kernel.Time;
using EpochSim.Kernel.Messaging;

namespace EpochSim.Kernel.Scheduling;

public readonly record struct ScheduledItem(SimTime Time, long Seq, IEvent Event);