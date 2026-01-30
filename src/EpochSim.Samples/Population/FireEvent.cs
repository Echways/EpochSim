using EpochSim.Kernel.Messaging;

namespace EpochSim.Samples.Population;

public sealed record FireEvent(int Damage) : IEvent
{
    public string Kind => "Fire";
}