namespace EpochSim.Execution.RunArtifacts;

public sealed class RunPaths(string rootDir, string runId)
{
    public const string EventsFileName = "events.jsonl";
    public const string TraceFileName = "trace.jsonl";
    public const string ProfileFileName = "profile.jsonl";
    public const string StateFingerprintFileName = "statefp.jsonl";
    public const string SnapshotsDirectoryName = "snapshots";
    public const string DumpsDirectoryName = "dumps";
    public const string MetaFileName = "meta.txt";
    public const string ManifestFileName = "manifest.json";
    public const string FailureReportFileName = "failure-report.json";
    public const string FailureSnapshotFileName = "failure-snapshot.json";

    public string RootDir { get; } = rootDir;
    public string RunId { get; } = runId;

    public string RunDir => Path.Combine(RootDir, RunId);
    public string EventsPath => Path.Combine(RunDir, EventsFileName);
    public string EventsPathGz => EventsPath + ".gz";
    public string TracePath => Path.Combine(RunDir, TraceFileName);
    public string TracePathGz => TracePath + ".gz";
    public string ProfilePath => Path.Combine(RunDir, ProfileFileName);
    public string ProfilePathGz => ProfilePath + ".gz";
    public string StateFpPath => Path.Combine(RunDir, StateFingerprintFileName);
    public string SnapshotsDir => Path.Combine(RunDir, SnapshotsDirectoryName);
    public string DumpsDir => Path.Combine(RunDir, DumpsDirectoryName);
    public string MetaPath => Path.Combine(RunDir, MetaFileName);
    public string ManifestPath => Path.Combine(RunDir, ManifestFileName);
    public string FailureReportPath => Path.Combine(RunDir, FailureReportFileName);
    public string FailureSnapshotPath => Path.Combine(RunDir, FailureSnapshotFileName);

    public string ResolveEventsPath()
    {
        if (File.Exists(EventsPath))
            return EventsPath;
        if (File.Exists(EventsPathGz))
            return EventsPathGz;
        return EventsPath;
    }

    public string ResolveTracePath()
    {
        if (File.Exists(TracePath))
            return TracePath;
        if (File.Exists(TracePathGz))
            return TracePathGz;
        return TracePath;
    }

    public string ResolveProfilePath()
    {
        if (File.Exists(ProfilePath))
            return ProfilePath;
        if (File.Exists(ProfilePathGz))
            return ProfilePathGz;
        return ProfilePath;
    }

    public void Ensure()
    {
        Directory.CreateDirectory(RunDir);
        Directory.CreateDirectory(SnapshotsDir);
        Directory.CreateDirectory(DumpsDir);
    }
}
