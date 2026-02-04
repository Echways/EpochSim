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
}
