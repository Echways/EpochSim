using EpochSim.Execution;
using EpochSim.Execution.Diagnostics;
using EpochSim.Kernel.Systems;
using EpochSim.Kernel.Validation;
using EpochSim.Serialization.EventLog;
using EpochSim.Serialization.State;

namespace EpochSim.Cli.Domain;

public interface IDomainAdapter
{
    string Name { get; }
    Type StateType { get; }
    IEventCodecV2 Codec { get; }
    object Serializer { get; }
    IEventPayloadFormatter PayloadFormatter { get; }
    object CreateInitialState();
    void ConfigureEngine(object engine);
    IReadOnlyList<object> CreateInvariants();
    IReadOnlyList<string> DescribeState(object state);
}

public interface IDomainAdapter<TState> : IDomainAdapter
{
    new IStateSerializer<TState> Serializer { get; }
    new TState CreateInitialState();
    void ConfigureEngine(SimulationEngine<TState> engine);
    new IReadOnlyList<IInvariant<TState>> CreateInvariants();
    IReadOnlyList<string> DescribeState(TState state);
}
