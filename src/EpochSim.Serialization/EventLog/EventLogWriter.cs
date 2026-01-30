using System.Text;

namespace EpochSim.Serialization.EventLog;

public sealed class EventLogWriter : IDisposable
{
    private readonly StreamWriter _writer;

    public EventLogWriter(string path, bool append = false)
    {
        var fs = new FileStream(path, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.Read);
        _writer = new StreamWriter(fs, new UTF8Encoding(false));
    }

    public void Write(long tick, string kind, string payload)
    {
        _writer.Write("{\"t\":");
        _writer.Write(tick);
        _writer.Write(",\"kind\":");
        _writer.Write(JsonEscape(kind));
        _writer.Write(",\"payload\":");
        _writer.Write(JsonEscape(payload));
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
