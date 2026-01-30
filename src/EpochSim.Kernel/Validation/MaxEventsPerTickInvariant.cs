using EpochSim.Kernel.Time;

namespace EpochSim.Kernel.Validation;

public sealed class MaxEventsPerTickInvariant<TState>(
    Func<int> eventsThisTickProvider,
    int maxEvents) : IInvariant<TState>
{
    public string Name => "MaxEventsPerTick";

    public bool Check(SimTime time, TState state, out string message)
    {
        var n = eventsThisTickProvider();
        if (n <= maxEvents)
        {
            message = "";
            return true;
        }

        message = $"EventsThisTick={n}, Max={maxEvents}";
        return false;
    }
}
