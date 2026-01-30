namespace EpochSim.Execution.RunArtifacts;

public sealed class RunPaths(string rootDir, string runId)
{
    public string RootDir { get; } = rootDir;
    public string RunId { get; } = runId;

    public string RunDir => Path.Combine(RootDir, RunId);
    public string EventsPath => Path.Combine(RunDir, "events.jsonl");
    public string TracePath => Path.Combine(RunDir, "trace.jsonl");
    public string SnapshotsDir => Path.Combine(RunDir, "snapshots");
    public string DumpsDir => Path.Combine(RunDir, "dumps");
    public string MetaPath => Path.Combine(RunDir, "meta.txt");

    public void Ensure()
    {
        Directory.CreateDirectory(RunDir);
        Directory.CreateDirectory(SnapshotsDir);
        Directory.CreateDirectory(DumpsDir);
    }
}
