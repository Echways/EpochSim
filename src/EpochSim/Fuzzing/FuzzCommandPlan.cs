using EpochSim.Kernel.Determinism;
using EpochSim.Kernel.Messaging;

namespace EpochSim.Fuzzing;

public sealed class FuzzCommandPlan
{
    private const ulong StreamConstant = 0xD1B54A32D192ED03UL;

    private readonly List<(long Tick, ICommand Command)> _entries = [];

    internal FuzzCommandPlan(ulong seed, long endTick)
    {
        Seed = seed;
        EndTick = endTick;
        Rng = new DeterministicRng(SplitMix64(seed ^ StreamConstant));
    }

    public ulong Seed { get; }
    public long EndTick { get; }
    public IRng Rng { get; }

    internal bool IsEmpty => _entries.Count == 0;

    public void Enqueue(long tick, ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentOutOfRangeException.ThrowIfNegative(tick);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(tick, EndTick);

        _entries.Add((tick, command));
    }

    internal void ScheduleInto(ScheduledCommandInbox inbox)
    {
        foreach (var (tick, command) in _entries)
            inbox.Enqueue(tick, command);
    }

    private static ulong SplitMix64(ulong value)
    {
        var z = value + 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
