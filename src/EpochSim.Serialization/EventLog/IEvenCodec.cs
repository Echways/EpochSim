using EpochSim.Kernel.Messaging;

namespace EpochSim.Serialization.EventLog;

public interface IEventCodec
{
    bool TryEncode(IEvent ev, out string kind, out string payload);
    bool TryDecode(string kind, string payload, out IEvent ev);
}