using System.IO.Compression;
using System.Text;
using System.Text.Encodings.Web;

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

    // Encodes the string as a safe JSON string value using JavaScriptEncoder.Default, which
    // correctly escapes all control characters < 0x20 (including \b, \f), embedded quotes,
    // backslashes, and Unicode line/paragraph separators U+2028 and U+2029.
    private void WriteEscaped(string s)
    {
        if (s.Length == 0) return;
        JavaScriptEncoder.Default.Encode(_writer, s, 0, s.Length);
    }
}
