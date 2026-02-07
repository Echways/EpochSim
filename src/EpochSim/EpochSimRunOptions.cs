using EpochSim.Execution.RunArtifacts;
using EpochRunId = EpochSim.Execution.RunArtifacts.RunId;

namespace EpochSim;

public sealed class EpochSimRunOptions
{
    public string RootDirectory
    {
        get;
        init
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("RootDirectory cannot be empty.", nameof(value));
            field = value;
        }
    } = "artifacts";

    public string RunId
    {
        get;
        init
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("RunId cannot be empty.", nameof(value));
            field = value;
        }
    } = EpochRunId.New();

    public bool Compression { get; init; }
}

public sealed class SnapshotRunOptions
{
    public long EveryTicks
    {
        get;
        init
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), "EveryTicks must be > 0.");
            field = value;
        }
    } = 50;
}

public sealed class StateFingerprintRunOptions
{
    public long EveryTicks
    {
        get;
        init
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), "EveryTicks must be > 0.");
            field = value;
        }
    } = 1;
}

public sealed class InvariantRunOptions
{
    public long CheckEveryTicks
    {
        get;
        init
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), "CheckEveryTicks must be > 0.");
            field = value;
        }
    } = 1;
}

public sealed class FailureArtifactsRunOptions
{
    public int TailSize
    {
        get;
        init
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), "TailSize must be >= 0.");
            field = value;
        }
    } = 200;
}
