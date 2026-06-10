using EpochSim.Kernel.Determinism;
using Xunit;

public sealed class RngTests
{
    [Fact]
    public void SameSeed_ProducesSameSequence_V1()
    {
        var a = new DeterministicRng(12345, RngVersion.V1);
        var b = new DeterministicRng(12345, RngVersion.V1);

        for (int i = 0; i < 100; i++)
            Assert.Equal(a.NextU64(), b.NextU64());
    }

    [Fact]
    public void SameSeed_ProducesSameSequence_V2()
    {
        var a = new DeterministicRng(12345, RngVersion.V2);
        var b = new DeterministicRng(12345, RngVersion.V2);

        for (int i = 0; i < 100; i++)
            Assert.Equal(a.NextU64(), b.NextU64());
    }

    [Fact]
    public void NextInt_V2_CoversAllBuckets()
    {
        var rng = new DeterministicRng(12345, RngVersion.V2);
        var buckets = new int[10];

        for (int i = 0; i < 10_000; i++)
            buckets[rng.NextInt(0, 10)]++;

        for (int i = 0; i < buckets.Length; i++)
            Assert.True(buckets[i] > 0, $"Bucket {i} was never hit.");
    }

    [Theory]
    [InlineData(5, 5)]
    [InlineData(10, 5)]
    [InlineData(0, -1)]
    public void NextInt_InvalidRange_ThrowsArgumentOutOfRangeWithParamName(int min, int max)
    {
        var rng = new DeterministicRng(1);
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(min, max));
        Assert.Equal("maxExclusive", ex.ParamName);
        Assert.Contains("maxExclusive", ex.Message);
        Assert.Contains("minInclusive", ex.Message);
    }

    [Fact]
    public void Seed0_SubstitutedWith_GoldenRatioConstant_SameAsExplicitConstant()
    {
        // Pins the seed=0 substitution invariant — any future change to the constant breaks this test.
        const ulong goldenRatio = 0x9E3779B97F4A7C15UL;
        var rng0 = new DeterministicRng(0);
        var rngGolden = new DeterministicRng(goldenRatio);

        for (int i = 0; i < 20; i++)
            Assert.Equal(rngGolden.NextU64(), rng0.NextU64());
    }

    [Fact]
    public void Seed0_ProducesNonDegenerateSequence()
    {
        var rng = new DeterministicRng(0);
        var values = new HashSet<ulong>();
        for (int i = 0; i < 100; i++)
            values.Add(rng.NextU64());

        Assert.Equal(100, values.Count);
    }

    [Fact]
    public void Seed0_DifferentFrom_Seed1()
    {
        var rng0 = new DeterministicRng(0);
        var rng1 = new DeterministicRng(1);

        var seq0 = Enumerable.Range(0, 10).Select(_ => rng0.NextU64()).ToArray();
        var seq1 = Enumerable.Range(0, 10).Select(_ => rng1.NextU64()).ToArray();

        Assert.False(seq0.SequenceEqual(seq1), "seed=0 and seed=1 must produce different sequences.");
    }
}
