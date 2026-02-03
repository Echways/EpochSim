using EpochSim.Kernel.Messaging;

namespace EpochSim.Serialization.EventLog;

public sealed class CompositeEventCodec(params IEventCodec[] codecs) : IEventCodec
{
    private readonly IEventCodec[] _codecs = codecs;

    public bool TryEncode(IEvent ev, out string kind, out string payload)
    {
        for (int i = 0; i < _codecs.Length; i++)
        {
            if (_codecs[i].TryEncode(ev, out kind, out payload))
                return true;
        }

        kind = "";
        payload = "";
        return false;
    }

    public bool TryDecode(string kind, string payload, out IEvent ev)
    {
        for (int i = 0; i < _codecs.Length; i++)
        {
            if (_codecs[i].TryDecode(kind, payload, out ev))
                return true;
        }

        return Fail(out ev);
    }

    private static bool Fail(out IEvent ev)
    {
        ev = default!;
        return false;
    }
}
