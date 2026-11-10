namespace EpochSim.Samples.Onboarding;

public sealed class Inventory
{
    private const int RestockOdds = 3;
    private const int RestockUnits = 4;
    private const int BulkOrderOdds = 400;
    private const int BulkOrderUnits = 5;

    public int Stock { get; private set; } = 5;

    public void Tick(Random random)
    {
        if (random.Next(0, RestockOdds) == 0)
            Stock += RestockUnits;

        var demand = random.Next(0, 3);
        if (Stock >= demand)
            Stock -= demand;

        if (random.Next(0, BulkOrderOdds) == 0)
            Stock -= BulkOrderUnits;
    }
}
