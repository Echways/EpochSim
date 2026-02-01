namespace EpochSim.Execution.Diagnostics;

public interface IEventPayloadFormatter
{
    bool TryFormat(string kind, string payload, out string formatted);
}