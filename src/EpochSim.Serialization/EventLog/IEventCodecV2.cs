using EpochSim.Kernel.Messaging;

namespace EpochSim.Serialization.EventLog;

public interface IEventCodecV2
{
    bool TryEncode(IEvent ev, out string kind, out string payloadJson);
    bool TryDecode(string kind, string payloadJson, out IEvent ev);
}
