namespace EpochSim.Execution;

public sealed class ExecutionOptions
{
    public bool EnableEventDispatchDuringTickBoundaryOnly { get; init; } = true;
}