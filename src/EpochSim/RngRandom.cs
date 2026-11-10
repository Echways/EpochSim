using EpochSim.Kernel.Determinism;

namespace EpochSim;

internal sealed class RngRandom(IRng rng) : Random
{
    public override int Next()
        => (int)NextBelow(int.MaxValue);

    public override int Next(int maxValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxValue);
        return (int)NextBelow((ulong)maxValue);
    }

    public override int Next(int minValue, int maxValue)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minValue, maxValue, nameof(minValue));
        return (int)NextInRange(minValue, maxValue);
    }

    public override long NextInt64()
        => (long)NextBelow(long.MaxValue);

    public override long NextInt64(long maxValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxValue);
        return (long)NextBelow((ulong)maxValue);
    }

    public override long NextInt64(long minValue, long maxValue)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minValue, maxValue, nameof(minValue));
        return NextInRange(minValue, maxValue);
    }

    public override double NextDouble()
        => rng.NextDouble01();

    public override float NextSingle()
        => (rng.NextU64() >> 40) * (1.0f / (1 << 24));

    public override void NextBytes(byte[] buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        NextBytes(buffer.AsSpan());
    }

    public override void NextBytes(Span<byte> buffer)
    {
        Span<byte> word = stackalloc byte[sizeof(ulong)];

        for (var offset = 0; offset < buffer.Length; offset += word.Length)
        {
            BitConverter.TryWriteBytes(word, rng.NextU64());
            word[..Math.Min(word.Length, buffer.Length - offset)].CopyTo(buffer[offset..]);
        }
    }

    protected override double Sample()
        => rng.NextDouble01();

    private long NextInRange(long minValue, long maxValue)
        => unchecked(minValue + (long)NextBelow(unchecked((ulong)maxValue - (ulong)minValue)));

    private ulong NextBelow(ulong range)
    {
        if (range == 0)
            return 0;

        var limit = ulong.MaxValue - (ulong.MaxValue % range);

        while (true)
        {
            var value = rng.NextU64();
            if (value < limit)
                return value % range;
        }
    }
}
