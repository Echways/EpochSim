using System.Text;
using System.IO.Compression;

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
        _writer.Write(",\"kind\":");
        _writer.Write(JsonEscape(kind));
        _writer.Write(",\"payload\":");
        _writer.Write(payloadJson);
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
        var builder = new StringBuilder(s.Length + 8);
        builder.Append('"');
        foreach (var ch in s)
        {
            builder.Append(ch switch
            {
                '"' => "\\\"",
                '\\' => "\\\\",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                _ => ch
            });
        }
        builder.Append('"');
        return builder.ToString();
    }
}
