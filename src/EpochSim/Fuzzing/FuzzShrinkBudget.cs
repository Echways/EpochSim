namespace EpochSim.Fuzzing;

internal readonly record struct FuzzShrinkBudget(int MaxAttempts, TimeSpan TimeLimit)
{
    public static FuzzShrinkBudget Default { get; } = new(MaxAttemptsLimit, TimeSpan.FromSeconds(TimeLimitSeconds));

    private const int MaxAttemptsLimit = 1000;
    private const int TimeLimitSeconds = 10;
}
