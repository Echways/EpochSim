namespace EpochSim.Execution.Diagnostics;

public sealed class CompositeEventPayloadFormatter(params IEventPayloadFormatter[] formatters) : IEventPayloadFormatter
{
    private readonly IEventPayloadFormatter[] _formatters = formatters;

    public bool TryFormat(string kind, string payload, out string formatted)
    {
        foreach (var formatter in _formatters)
        {
            if (formatter.TryFormat(kind, payload, out formatted))
                return true;
        }

        formatted = "";
        return false;
    }
}
