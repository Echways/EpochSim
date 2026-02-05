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
        _writer.Write(",\"system\":\"");
        WriteEscaped(r.SystemName);
        _writer.Write('"');
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
