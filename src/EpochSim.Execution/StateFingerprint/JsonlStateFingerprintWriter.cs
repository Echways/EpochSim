using System.Text;
using EpochSim.Serialization.EventLog;

namespace EpochSim.Execution.StateFingerprint;

public sealed class JsonlStateFingerprintWriter : IDisposable, IStateFingerprintSink
{
    private readonly StreamWriter _w;

    public JsonlStateFingerprintWriter(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        _w = new StreamWriter(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read), new UTF8Encoding(false));
    }

    public void OnRecord(long tick, string hash)
    {
        _w.Write("{\"t\":");
        _w.Write(tick);
        _w.Write(",\"h\":\"");
        _w.Write(hash);
        _w.WriteLine("\"}");
    }

    public void Dispose() => _w.Dispose();

    public static Dictionary<long, string> ReadAll(string path)
    {
        var dict = new Dictionary<long, string>();
        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var t = EventLogLine.ReadLongField(line, "\"t\":");
            var h = EventLogLine.ReadStringField(line, "\"h\":");
            dict[t] = h;
        }
        return dict;
    }
}
