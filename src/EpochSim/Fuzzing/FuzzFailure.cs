using System.Text;
using EpochSim.Serialization.State;

namespace EpochSim.Fuzzing;

public sealed record FuzzFailure(
    ulong Seed,
    FuzzFailureKind Kind,
    long Tick,
    string Summary,
    IReadOnlyList<StateChange> StateDiff,
    string? ArtifactsDir,
    string ReproCode)
{
    public int OriginalCommandCount { get; init; }
    public IReadOnlyList<int>? MinimalCommandIndices { get; init; }
    public IReadOnlyList<FuzzCommandRef> MinimalCommands { get; init; } = [];
    public int ShrinkAttempts { get; init; }
    public bool ShrinkBudgetExhausted { get; init; }
    public string? ShrinkSkipReason { get; init; }
    public long ReproTicks { get; init; }

    public string ToText()
    {
        var text = new StringBuilder();
        text.Append('[').Append(Kind).Append("] seed=").Append(Seed).Append(" tick=").Append(Tick).AppendLine();
        text.Append("  ").AppendLine(Summary);

        AppendShrink(text);

        if (StateDiff.Count > 0)
        {
            text.AppendLine("  StateDiff:");
            foreach (var line in StateDiffFormatter.Format(StateDiff))
                text.Append("    ").AppendLine(line);
        }

        if (ShrinkSkipReason is not null)
            text.Append("  Shrink skipped: ").AppendLine(ShrinkSkipReason);

        if (ArtifactsDir is not null)
            text.Append("  Artifacts: ").AppendLine(ArtifactsDir);

        text.Append("  Repro: ").Append(ReproCode);
        return text.ToString();
    }

    private void AppendShrink(StringBuilder text)
    {
        if (MinimalCommandIndices is not { } indices)
            return;

        text.Append("  Commands: ").Append(OriginalCommandCount).Append(" -> ").Append(indices.Count);
        text.Append(ShrinkBudgetExhausted
            ? $" (shrink budget exhausted after {ShrinkAttempts} attempts, result may not be minimal)"
            : $" (shrunk in {ShrinkAttempts} attempts)");
        text.AppendLine();

        foreach (var command in MinimalCommands)
            text.Append("    #").Append(command.Index).Append(" tick ").Append(command.Tick).Append(": ").AppendLine(command.Type);
    }
}
