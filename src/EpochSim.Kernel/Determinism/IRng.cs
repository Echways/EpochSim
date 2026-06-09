namespace EpochSim.Kernel.Determinism;

public interface IRng
{
    ulong NextU64();
    int NextInt(int minInclusive, int maxExclusive);
    double NextDouble01();
}
