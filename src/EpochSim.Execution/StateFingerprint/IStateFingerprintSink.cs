namespace EpochSim.Execution.StateFingerprint;

public interface IStateFingerprintSink
{
    void OnRecord(long tick, string hash);
}