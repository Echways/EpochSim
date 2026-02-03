using EpochSim.Kernel.Messaging;
using EpochSim.Kernel.Time;

namespace EpochSim.Samples.Population;

public sealed class GrowPopulationHandler : ICommandHandler<WorldState, GrowPopulationCommand>
{
    public void Handle(WorldState state, GrowPopulationCommand command, IEventBuffer events)
        => events.Emit(new PopulationDeltaEvent(command.Delta));
}

public sealed class ScheduleFireHandler : ICommandHandler<WorldState, ScheduleFireCommand>
{
    public void Handle(WorldState state, ScheduleFireCommand command, IEventBuffer events)
        => events.Emit(new FireScheduledEvent(new SimTime(command.AtTick), command.Damage));
}

public sealed record PopulationDeltaEvent(int Delta) : IEvent
{
    public string Kind => "PopulationDelta";
}

public sealed record FireScheduledEvent(SimTime At, int Damage) : IEvent
{
    public string Kind => "FireScheduled";
}
