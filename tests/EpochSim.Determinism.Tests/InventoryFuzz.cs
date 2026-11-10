using EpochSim;
using EpochSim.Fuzzing;
using EpochSim.Samples.Onboarding;

namespace MyApp;

public static class InventoryFuzz
{
    public static void Configure(FuzzBuilder<Inventory> fuzz)
        => fuzz.Name("inventory")
            .State(() => new Inventory())
            .Step((inventory, context) => inventory.Tick(context.Rng.AsRandom()))
            .Invariant("StockNonNegative", inventory => inventory.Stock >= 0)
            .Seeds(1, 1000)
            .Ticks(500);
}
