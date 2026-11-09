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
    public string ToText()
    {
        var text = new StringBuilder();
        text.Append('[').Append(Kind).Append("] seed=").Append(Seed).Append(" tick=").Append(Tick).AppendLine();
        text.Append("  ").AppendLine(Summary);

        if (StateDiff.Count > 0)
        {
            text.AppendLine("  StateDiff:");
            foreach (var line in StateDiffFormatter.Format(StateDiff))
                text.Append("    ").AppendLine(line);
        }

        if (ArtifactsDir is not null)
            text.Append("  Artifacts: ").AppendLine(ArtifactsDir);

        text.Append("  Repro: ").Append(ReproCode);
        return text.ToString();
    }
}
