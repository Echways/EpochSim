using System.Text;

namespace EpochSim.Serialization.Snapshots;

public static class SnapshotWriter
{
    public static void Write(string path, long tick, string stateJson)
    {
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        using var w = new StreamWriter(fs, new UTF8Encoding(false));

        w.Write("{\"t\":");
        w.Write(tick);
        w.Write(",\"state\":");
        w.Write(stateJson);
        w.WriteLine("}");
    }
}
