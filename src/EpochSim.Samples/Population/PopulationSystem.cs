using EpochSim.Kernel.Messaging;
using EpochSim.Kernel.Systems;
using EpochSim.Kernel.Time;

namespace EpochSim.Samples.Population;

public sealed class PopulationSystem : ISystem<WorldState>
{
    public string Name => "Population";

    public void Tick(TickContext<WorldState> ctx)
    {
        if (ctx.Time.Tick % 10 == 0 && ctx.Time.Tick != 0)
            ctx.Commands.Enqueue(new GrowPopulationCommand(Delta: 5));

        if (ctx.Time.Tick == 25)
            ctx.Commands.Enqueue(new ScheduleFireCommand(AtTick: 30, Damage: 20));
    }

    public void Handle(EventContext<WorldState> ctx, IEvent ev)
    {
        switch (ev)
        {
            case PopulationDeltaEvent pd:
                ctx.State.Population += pd.Delta;
                break;

            case FireScheduledEvent fs:
                ctx.Scheduler.ScheduleAt(fs.At, new FireEvent(fs.Damage));
                break;

            case FireEvent fire:
                ctx.State.Fires += 1;
                ctx.State.Population = Math.Max(0, ctx.State.Population - fire.Damage);
                break;
        }
    }
}
