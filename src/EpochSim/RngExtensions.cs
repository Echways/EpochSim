using System.Runtime.CompilerServices;
using EpochSim.Kernel.Determinism;

namespace EpochSim;

public static class RngExtensions
{
    private static readonly ConditionalWeakTable<IRng, Random> Adapters = [];

    public static Random AsRandom(this IRng rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        return Adapters.GetValue(rng, static stream => new RngRandom(stream));
    }
}
