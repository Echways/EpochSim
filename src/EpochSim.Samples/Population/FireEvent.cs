using EpochSim.Kernel.Messaging;

namespace EpochSim.Samples.Population;

[MessageKind("Fire")]
public sealed record FireEvent(int Damage) : IEvent;
