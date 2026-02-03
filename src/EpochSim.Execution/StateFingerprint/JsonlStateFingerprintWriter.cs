using System.Text;
using System.Text.Json;

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
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (!root.TryGetProperty("t", out var tProp) || !root.TryGetProperty("h", out var hProp))
                continue;

            var t = tProp.GetInt64();
            var h = hProp.GetString() ?? "";
            dict[t] = h;
        }
        return dict;
    }
}
