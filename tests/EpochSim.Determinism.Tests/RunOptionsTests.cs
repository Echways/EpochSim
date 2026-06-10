using EpochSim.Execution;
using Xunit;

public sealed class RunOptionsTests
{
    [Fact]
    public void MaxEventDispatchesPerTick_DefaultIs100k()
    {
        var opts = new RunOptions();
        Assert.Equal(100_000, opts.MaxEventDispatchesPerTick);
    }

    [Fact]
    public void MaxEventsPerTick_ObsoleteAlias_ReadsAndWritesMaxEventDispatchesPerTick()
    {
#pragma warning disable CS0618
        var opts = new RunOptions { MaxEventsPerTick = 500 };
        Assert.Equal(500, opts.MaxEventDispatchesPerTick);
        Assert.Equal(500, opts.MaxEventsPerTick);
#pragma warning restore CS0618
    }

    [Fact]
    public void MaxEventDispatchesPerTick_ZeroOrNegative_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new RunOptions { MaxEventDispatchesPerTick = 0 });
        Assert.Throws<InvalidOperationException>(() => new RunOptions { MaxEventDispatchesPerTick = -1 });
    }
}
