using EpochSim.Kernel.Messaging;

namespace EpochSim.Samples.Population;

[MessageKind("GrowPopulation")]
public sealed record GrowPopulationCommand(int Delta) : ICommand;

[MessageKind("ScheduleFire")]
public sealed record ScheduleFireCommand(long AtTick, int Damage) : ICommand;
