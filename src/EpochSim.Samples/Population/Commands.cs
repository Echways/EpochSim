using EpochSim.Kernel.Messaging;

namespace EpochSim.Samples.Population;

public sealed record GrowPopulationCommand(int Delta) : ICommand
{
    public string Kind => "GrowPopulation";
}

public sealed record ScheduleFireCommand(long AtTick, int Damage) : ICommand
{
    public string Kind => "ScheduleFire";
}