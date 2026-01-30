using EpochSim.Kernel.Messaging;
using EpochSim.Kernel.Time;

namespace EpochSim.Samples.Population;

public sealed class GrowPopulationHandler : ICommandHandler<WorldState>
{
    public Type CommandType => typeof(GrowPopulationCommand);

    public void Handle(WorldState state, ICommand command, IEventBuffer events)
    {
        var c = (GrowPopulationCommand)command;
        events.Emit(new PopulationDeltaEvent(c.Delta));
    }
}

public sealed class ScheduleFireHandler : ICommandHandler<WorldState>
{
    public Type CommandType => typeof(ScheduleFireCommand);

    public void Handle(WorldState state, ICommand command, IEventBuffer events)
    {
        var c = (ScheduleFireCommand)command;
        events.Emit(new FireScheduledEvent(new SimTime(c.AtTick), c.Damage));
    }
}

public sealed record PopulationDeltaEvent(int Delta) : IEvent
{
    public string Kind => "PopulationDelta";
}

public sealed record FireScheduledEvent(SimTime At, int Damage) : IEvent
{
    public string Kind => "FireScheduled";
}
