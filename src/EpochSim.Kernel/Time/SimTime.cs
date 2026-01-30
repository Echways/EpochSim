namespace EpochSim.Kernel.Time;

public readonly record struct SimTime(long Tick)
{
    public static SimTime Zero => new(0);
    public SimTime AddTicks(long delta) => new(Tick + delta);
    public override string ToString() => $"t={Tick}";
}