using System.IO.Compression;
using System.Text;

namespace EpochSim.Serialization.EventLog;

public sealed class EventLogWriter : IDisposable
{
    private readonly StreamWriter _writer;

    public EventLogWriter(string path, bool append = false)
    {
        var fileStream = new FileStream(path, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.Read);
        Stream stream = fileStream;
        if (path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
            stream = new GZipStream(fileStream, CompressionLevel.Optimal);

        _writer = new StreamWriter(stream, new UTF8Encoding(false));
    }

    public void Write(long tick, string kind, string payloadJson)
    {
        _writer.Write("{\"t\":");
        _writer.Write(tick);
        _writer.Write(",\"kind\":\"");
        WriteEscaped(kind);
        _writer.Write("\",\"payload\":");
        _writer.Write(payloadJson.AsSpan());
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
