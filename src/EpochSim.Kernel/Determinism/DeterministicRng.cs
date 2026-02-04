namespace EpochSim.Kernel.Determinism;

public sealed class DeterministicRng(ulong seed, RngVersion version = RngVersion.V2) : IRng
{
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

    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive) throw new ArgumentOutOfRangeException();
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
