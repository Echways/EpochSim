using EpochSim.Execution;

namespace EpochSim;

public static class SimulationEngineInboxExtensions
{
    extension<TState>(SimulationEngine<TState> engine)
    {
        public void AttachInbox(CommandInbox inbox, string systemName = "CommandInbox")
        {
            ArgumentNullException.ThrowIfNull(inbox);
            ArgumentException.ThrowIfNullOrWhiteSpace(systemName);

            engine.AddSystem(
                systemName,
                tick: ctx => inbox.DrainTo(ctx.Commands));
        }

        public void AttachScheduledInbox(ScheduledCommandInbox inbox, string systemName = "ScheduledCommandInbox")
        {
            ArgumentNullException.ThrowIfNull(inbox);
            ArgumentException.ThrowIfNullOrWhiteSpace(systemName);

            engine.AddSystem(
                systemName,
                tick: ctx => inbox.DrainAt(ctx.Time, ctx.Commands));
        }
    }
}
