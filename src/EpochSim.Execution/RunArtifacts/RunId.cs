namespace EpochSim.Execution.RunArtifacts;

public static class RunId
{
    public static string New() => DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8];
}