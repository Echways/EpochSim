using System.IO.Compression;
using System.Text;

namespace EpochSim.Observability.Tracing;

public sealed class JsonlTraceWriter : IDisposable
{
    private readonly StreamWriter _writer;

    public JsonlTraceWriter(string path, bool append = false)
    {
        var fileStream = new FileStream(path, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.Read);
        Stream stream = fileStream;
        if (path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
            stream = new GZipStream(fileStream, CompressionLevel.Optimal);

        _writer = new StreamWriter(stream, new UTF8Encoding(false));
    }

    public void Write(in TraceRecord r)
    {
        var detail = r.Detail;
        var dur = r.DurationTicks is null ? "null" : r.DurationTicks.Value.ToString();

        _writer.Write("{\"t\":");
        _writer.Write(r.Time.Tick);
        _writer.Write(",\"type\":\"");
        WriteEscaped(r.Type);
        _writer.Write("\",\"name\":\"");
        WriteEscaped(r.Name);
        _writer.Write('"');
        _writer.Write(",\"dur\":");
        _writer.Write(dur);
        _writer.Write(",\"detail\":");
        if (detail is null)
        {
            _writer.Write("null");
        }
        else
        {
            _writer.Write('"');
            WriteEscaped(detail);
            _writer.Write('"');
        }
        _writer.WriteLine("}");
    }

    public void Flush() => _writer.Flush();

    public void Dispose()
    {
        _writer.Flush();
        _writer.Dispose();
    }

    private void WriteEscaped(ReadOnlySpan<char> s)
    {
        foreach (var ch in s)
        {
            switch (ch)
            {
                case '"':
                    _writer.Write("\\\"");
                    break;
                case '\\':
                    _writer.Write("\\\\");
                    break;
                case '\n':
                    _writer.Write("\\n");
                    break;
                case '\r':
                    _writer.Write("\\r");
                    break;
                case '\t':
                    _writer.Write("\\t");
                    break;
                default:
                    _writer.Write(ch);
                    break;
            }
        }
    }
}
