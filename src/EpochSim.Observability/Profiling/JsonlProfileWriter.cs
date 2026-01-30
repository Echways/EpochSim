using System.Text;

namespace EpochSim.Observability.Profiling;

public sealed class JsonlProfileWriter : IDisposable
{
    private readonly StreamWriter _writer;

    public JsonlProfileWriter(string path, bool append = false)
    {
        var fs = new FileStream(path, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.Read);
        _writer = new StreamWriter(fs, new UTF8Encoding(false));
    }

    public void Write(in ProfileRecord r)
    {
        _writer.Write("{\"t\":");
        _writer.Write(r.Time.Tick);
        _writer.Write(",\"system\":\"");
        _writer.Write(JsonEscape(r.SystemName));
        _writer.Write("\",\"elapsed\":");
        _writer.Write(r.ElapsedStopwatchTicks);
        _writer.WriteLine("}");
    }

    public void Flush() => _writer.Flush();

    public void Dispose()
    {
        _writer.Flush();
        _writer.Dispose();
    }

    private static string JsonEscape(string s)
    {
        var sb = new StringBuilder(s.Length + 8);
        sb.Append('"');
        foreach (var ch in s)
        {
            sb.Append(ch switch
            {
                '"' => "\\\"",
                '\\' => "\\\\",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                _ => ch
            });
        }
        sb.Append('"');
        return sb.ToString();
    }
}
