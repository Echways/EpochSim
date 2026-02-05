using System.Text;
using System.Text.Json;

namespace EpochSim.Execution.StateFingerprint;

public sealed class JsonlStateFingerprintWriter : IDisposable, IStateFingerprintSink
{
    private readonly StreamWriter _writer;

    public JsonlStateFingerprintWriter(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        _writer = new StreamWriter(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read), new UTF8Encoding(false));
    }

    public void OnRecord(long tick, string hash)
    {
        _writer.Write("{\"t\":");
        _writer.Write(tick);
        _writer.Write(",\"h\":\"");
        _writer.Write(hash.AsSpan());
        _writer.WriteLine("\"}");
    }

    public void Dispose() => _writer.Dispose();

    public static Dictionary<long, string> ReadAll(string path)
    {
        var records = new Dictionary<long, string>();
        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (!root.TryGetProperty("t", out var tProp) || !root.TryGetProperty("h", out var hProp))
                continue;

            var tick = tProp.GetInt64();
            var hash = hProp.GetString() ?? "";
            records[tick] = hash;
        }
        return records;
    }
}
