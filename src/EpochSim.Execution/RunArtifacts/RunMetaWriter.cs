using System.Text;

namespace EpochSim.Execution.RunArtifacts;

public static class RunMetaWriter
{
    public static void Write(RunPaths paths, string mode, ulong seed, long endTick, long snapEvery)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"runId={paths.RunId}");
        sb.AppendLine($"utc={DateTime.UtcNow:O}");
        sb.AppendLine($"mode={mode}");
        sb.AppendLine($"seed={seed}");
        sb.AppendLine($"endTick={endTick}");
        sb.AppendLine($"snapEvery={snapEvery}");
        File.WriteAllText(paths.MetaPath, sb.ToString());
    }
}
