using System.Text;

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
            var t = ReadLongField(line, "\"t\":");
            var h = ReadStringField(line, "\"h\":\"");
            dict[t] = h;
        }
        return dict;
    }

    private static long ReadLongField(string line, string field)
    {
        var i = line.IndexOf(field, StringComparison.Ordinal);
        if (i < 0) throw new FormatException("Missing field");
        i += field.Length;
        while (i < line.Length && char.IsWhiteSpace(line[i])) i++;

        var end = i;
        while (end < line.Length && (char.IsDigit(line[end]) || line[end] == '-')) end++;

        if (!long.TryParse(line.AsSpan(i, end - i), out var v))
            throw new FormatException("Bad number");

        return v;
    }

    private static string ReadStringField(string line, string fieldPrefix)
    {
        var i = line.IndexOf(fieldPrefix, StringComparison.Ordinal);
        if (i < 0) throw new FormatException("Missing field");
        i += fieldPrefix.Length;

        var end = line.IndexOf('"', i);
        if (end < 0) throw new FormatException("Bad string");

        return line.Substring(i, end - i);
    }
}
