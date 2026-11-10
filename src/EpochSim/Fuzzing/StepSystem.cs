using EpochSim.Kernel.Systems;
using Microsoft.Extensions.Time.Testing;

namespace EpochSim.Fuzzing;

internal sealed class StepSystem<TState>(IReadOnlyList<Action<TState, StepContext>> steps)
{
    private readonly FakeTimeProvider _clock = new(StepContext.ClockStart);

    public void Tick(TickContext<TState> ctx)
    {
        _clock.SetUtcNow(StepContext.ClockStart + ctx.Time.Tick * StepContext.TickDuration);
        var context = new StepContext(ctx.Rng, ctx.Time.Tick, _clock);

        foreach (var step in steps)
            step(ctx.State, context);
    }
}
