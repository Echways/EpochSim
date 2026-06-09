namespace EpochSim.Kernel.Determinism;

public sealed class DeterministicRng(ulong seed, RngVersion version = RngVersion.V2) : IRng
{
    /// <remarks>
    /// When <paramref name="seed"/> is 0 the generator is seeded with a well-known non-zero constant
    /// (0x9E3779B97F4A7C15) so that all seeds produce distinct, non-degenerate sequences.
    /// </remarks>
    private ulong _state = seed != 0 ? seed : 0x9E3779B97F4A7C15UL;
    private readonly RngVersion _version = version;

    public ulong NextU64()
    {
        var value = _state;
        value ^= value >> 12;
        value ^= value << 25;
        value ^= value >> 27;
        _state = value;
        return value * 2685821657736338717UL;
    }

    /// <summary>
    /// Returns a deterministic integer in [<paramref name="minInclusive"/>, <paramref name="maxExclusive"/>).
    /// </summary>
    /// <param name="minInclusive">The inclusive lower bound.</param>
    /// <param name="maxExclusive">The exclusive upper bound. Must be strictly greater than <paramref name="minInclusive"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="maxExclusive"/> is less than or equal to <paramref name="minInclusive"/>.
    /// </exception>
    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
            throw new ArgumentOutOfRangeException(nameof(maxExclusive),
                $"maxExclusive ({maxExclusive}) must be greater than minInclusive ({minInclusive}).");
        var range = (ulong)(maxExclusive - minInclusive);
        var value = _version == RngVersion.V1
            ? NextU64() % range
            : NextU64Unbiased(range);
        return (int)value + minInclusive;
    }

    public double NextDouble01() => (NextU64() >> 11) * (1.0 / (1UL << 53));

    private ulong NextU64Unbiased(ulong range)
    {
        var limit = ulong.MaxValue - (ulong.MaxValue % range);
        while (true)
        {
            var value = NextU64();
            if (value < limit)
                return value % range;
        }
    }
}
