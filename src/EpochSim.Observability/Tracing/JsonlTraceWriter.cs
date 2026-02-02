using System.Text;

namespace EpochSim.Observability.Tracing;

public sealed class JsonlTraceWriter : IDisposable
{
    private readonly StreamWriter _writer;

    public JsonlTraceWriter(string path, bool append = false)
    {
        var fs = new FileStream(path, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.Read);
        _writer = new StreamWriter(fs, new UTF8Encoding(false));
    }

    public void Write(in TraceRecord r)
    {
        var detail = r.Detail is null ? "null" : JsonEscape(r.Detail);
        var dur = r.DurationTicks is null ? "null" : r.DurationTicks.Value.ToString();

        _writer.Write("{\"t\":");
        _writer.Write(r.Time.Tick);
        _writer.Write(",\"type\":");
        _writer.Write(JsonEscape(r.Type));
        _writer.Write(",\"name\":");
        _writer.Write(JsonEscape(r.Name));
        _writer.Write(",\"dur\":");
        _writer.Write(dur);
        _writer.Write(",\"detail\":");
        _writer.Write(detail);
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
