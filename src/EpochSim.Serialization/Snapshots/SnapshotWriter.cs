using System.Text;

namespace EpochSim.Serialization.Snapshots;

public static class SnapshotWriter
{
    public static void Write(string path, long tick, string stateJson)
    {
        using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(fileStream, new UTF8Encoding(false));

        writer.Write("{\"t\":");
        writer.Write(tick);
        writer.Write(",\"state\":");
        writer.Write(stateJson);
        writer.WriteLine("}");
    }
}
