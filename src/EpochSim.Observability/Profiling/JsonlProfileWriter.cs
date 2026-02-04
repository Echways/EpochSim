using System.Text;

namespace EpochSim.Observability.Profiling;

public sealed class JsonlProfileWriter : IDisposable
{
    private readonly StreamWriter _writer;

    public JsonlProfileWriter(string path, bool append = false)
    {
        var fileStream = new FileStream(path, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.Read);
        _writer = new StreamWriter(fileStream, new UTF8Encoding(false));
    }

    public void Write(in ProfileRecord r)
    {
        _writer.Write("{\"t\":");
        _writer.Write(r.Time.Tick);
        _writer.Write(",\"system\":");
        _writer.Write(JsonEscape(r.SystemName));
        _writer.Write(",\"elapsed\":");
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
