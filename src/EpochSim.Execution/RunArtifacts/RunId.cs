namespace EpochSim.Execution.RunArtifacts;

public static class RunId
{
    public static string New()
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var suffix = Guid.NewGuid().ToString("N")[..8];
        return $"{timestamp}-{suffix}";
    }
}
