using EpochSim;
using EpochSim.Execution;
using EpochSim.Kernel.Time;
using Xunit;

public sealed class PresetTests
{
    private sealed class CounterState
    {
        public int Count { get; set; }
    }

    [Fact]
    public void RecommendedRun_MutatingSystem_DoesNotThrow()
    {
        var root = Path.Combine(Path.GetTempPath(), $"epoch-preset-rec-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var state = new CounterState();
            var engine = new SimulationEngine<CounterState>();
            engine.AddSystem("counter", tick: ctx => ctx.State.Count++);

            using var run = Epoch.RecommendedRun(state, rootDir: root);
            run.RunTicks(engine, seed: 1, endTickInclusive: 10);

            Assert.Equal(11, state.Count);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DebugRun_MutatingSystem_ThrowsInvalidOperation()
    {
        var root = Path.Combine(Path.GetTempPath(), $"epoch-preset-dbg-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var state = new CounterState();
            var engine = new SimulationEngine<CounterState>();
            engine.AddSystem("counter", tick: ctx => ctx.State.Count++);

            using var run = Epoch.DebugRun(state, rootDir: root);
            Assert.Throws<InvalidOperationException>(() =>
                run.RunTicks(engine, seed: 1, endTickInclusive: 10));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void QuickRun_MutatingSystem_DoesNotThrow()
    {
        var root = Path.Combine(Path.GetTempPath(), $"epoch-preset-quick-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var state = new CounterState();
            var engine = new SimulationEngine<CounterState>();
            engine.AddSystem("counter", tick: ctx => ctx.State.Count++);

            using var run = Epoch.QuickRun(state, rootDir: root);
            run.RunTicks(engine, seed: 1, endTickInclusive: 5);

            Assert.Equal(6, state.Count);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
