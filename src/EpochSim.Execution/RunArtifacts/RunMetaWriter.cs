using System.Text;

namespace EpochSim.Execution.RunArtifacts;

public static class RunMetaWriter
{
    public static void Write(RunPaths paths, string mode, ulong seed, long endTick, long snapEvery, long fingerprintEvery = 1)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"runId={paths.RunId}");
        builder.AppendLine($"utc={DateTime.UtcNow:O}");
        builder.AppendLine($"mode={mode}");
        builder.AppendLine($"seed={seed}");
        builder.AppendLine($"endTick={endTick}");
        builder.AppendLine($"snapEvery={snapEvery}");
        builder.AppendLine($"fingerprintEvery={fingerprintEvery}");
        File.WriteAllText(paths.MetaPath, builder.ToString());
    }
}
