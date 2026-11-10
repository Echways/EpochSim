using EpochSim;
using EpochSim.Kernel.Determinism;

public sealed class RngRandomTests
{
    [Fact]
    public void AsRandom_SameStream_ProducesSameSequence()
    {
        var first = new DeterministicRng(42).AsRandom();
        var second = new DeterministicRng(42).AsRandom();

        Assert.Equal(Sample(first), Sample(second));
        Assert.NotEqual(Sample(new DeterministicRng(43).AsRandom()), Sample(new DeterministicRng(42).AsRandom()));
    }

    [Fact]
    public void AsRandom_ConsumesTheUnderlyingStream()
    {
        var rng = new DeterministicRng(7);
        var mirror = new DeterministicRng(7);

        var drawn = rng.AsRandom().Next(0, 1000);

        Assert.Equal(mirror.NextInt(0, 1000), drawn);
        Assert.Equal(mirror.NextU64(), rng.NextU64());
    }

    [Fact]
    public void AsRandom_ReturnsTheSameAdapterForTheSameStream()
    {
        var rng = new DeterministicRng(1);

        Assert.Same(rng.AsRandom(), rng.AsRandom());
        Assert.NotSame(rng.AsRandom(), new DeterministicRng(1).AsRandom());
    }

    [Fact]
    public void AsRandom_RespectsRangesOfEveryOverload()
    {
        var random = new DeterministicRng(99).AsRandom();

        for (var i = 0; i < 2000; i++)
        {
            Assert.InRange(random.Next(), 0, int.MaxValue - 1);
            Assert.InRange(random.Next(10), 0, 9);
            Assert.InRange(random.Next(-5, 5), -5, 4);
            Assert.InRange(random.Next(int.MinValue, int.MaxValue), int.MinValue, int.MaxValue - 1);
            Assert.InRange(random.NextInt64(), 0L, long.MaxValue - 1);
            Assert.InRange(random.NextInt64(100L), 0L, 99L);
            Assert.InRange(random.NextInt64(long.MinValue, long.MaxValue), long.MinValue, long.MaxValue - 1);
            Assert.InRange(random.NextDouble(), 0.0, 0.9999999999999999);
            Assert.InRange(random.NextSingle(), 0f, 0.99999994f);
        }
    }

    [Fact]
    public void AsRandom_CoversTheWholeRange()
    {
        var random = new DeterministicRng(5).AsRandom();

        var seen = Enumerable.Range(0, 500).Select(_ => random.Next(0, 4)).Distinct().Order();

        Assert.Equal([0, 1, 2, 3], seen);
    }

    [Fact]
    public void AsRandom_DegenerateRangesFollowRandomSemantics()
    {
        var random = new DeterministicRng(3).AsRandom();

        Assert.Equal(0, random.Next(0));
        Assert.Equal(7, random.Next(7, 7));
        Assert.Equal(9L, random.NextInt64(9L, 9L));
        Assert.Throws<ArgumentOutOfRangeException>(() => random.Next(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => random.Next(5, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => random.NextInt64(-1L));
        Assert.Throws<ArgumentOutOfRangeException>(() => random.NextInt64(5L, 4L));
    }

    [Fact]
    public void AsRandom_FillsBuffersDeterministically()
    {
        var first = new byte[19];
        var second = new byte[19];
        var span = new byte[19];

        new DeterministicRng(11).AsRandom().NextBytes(first);
        new DeterministicRng(11).AsRandom().NextBytes(second);
        new DeterministicRng(11).AsRandom().NextBytes(span.AsSpan());

        Assert.Equal(first, second);
        Assert.Equal(first, span);
        Assert.Contains(first, value => value != 0);
    }

    [Fact]
    public void AsRandom_WorksWithFrameworkHelpersThatTakeRandom()
    {
        var items = Enumerable.Range(0, 20).ToArray();
        var other = items.ToArray();

        new DeterministicRng(8).AsRandom().Shuffle(items);
        new DeterministicRng(8).AsRandom().Shuffle(other);

        Assert.Equal(items, other);
        Assert.NotEqual(Enumerable.Range(0, 20), items);
        Assert.Equal(Enumerable.Range(0, 20), items.Order());
    }

    [Fact]
    public void AsRandom_NullStream_Throws()
        => Assert.Throws<ArgumentNullException>(() => ((IRng)null!).AsRandom());

    private static List<int> Sample(Random random)
        => Enumerable.Range(0, 16).Select(_ => random.Next(0, 1_000_000)).ToList();
}
