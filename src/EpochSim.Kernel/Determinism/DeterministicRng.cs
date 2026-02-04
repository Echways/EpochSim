namespace EpochSim.Kernel.Determinism;

public sealed class DeterministicRng(ulong seed) : IRng
{
    private ulong _state = seed != 0 ? seed : 0x9E3779B97F4A7C15UL;

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
        return (int)(NextU64() % range) + minInclusive;
    }

    public double NextDouble01() => (NextU64() >> 11) * (1.0 / (1UL << 53));
}
