using EpochSim.Execution;
using EpochSim.Execution.Diagnostics;
using EpochSim.Kernel.Validation;
using EpochSim.Samples.Fuzzing;
using EpochSim.Serialization.EventLog;
using EpochSim.Serialization.State;

namespace EpochSim.Cli.Domain;

public sealed class FuzzBugDomainAdapter : IDomainAdapter<JobQueueState>
{
    public string Name => "fuzzbug";
    public Type StateType => typeof(JobQueueState);
    public IEventCodecV2 Codec { get; } = new JsonEventCodecBuilder().Build();
    public IStateSerializer<JobQueueState> Serializer { get; } = new JsonStateSerializer<JobQueueState>();
    public IEventPayloadFormatter PayloadFormatter { get; } = new JsonEventPayloadFormatter();

    object IDomainAdapter.Serializer => Serializer;

    public JobQueueState CreateInitialState() => new();

    object IDomainAdapter.CreateInitialState() => CreateInitialState();

    public void ConfigureEngine(SimulationEngine<JobQueueState> engine)
        => engine.AddSystem(new JobQueueSystem());

    void IDomainAdapter.ConfigureEngine(object engine)
        => ConfigureEngine((SimulationEngine<JobQueueState>)engine);

    public IReadOnlyList<IInvariant<JobQueueState>> CreateInvariants()
        => [new QueueNonNegativeInvariant()];

    IReadOnlyList<object> IDomainAdapter.CreateInvariants()
        => CreateInvariants().Cast<object>().ToList();

    public IReadOnlyList<string> DescribeState(JobQueueState state)
        => [$"Pending={state.Pending}", $"Completed={state.Completed}"];

    IReadOnlyList<string> IDomainAdapter.DescribeState(object state)
        => DescribeState((JobQueueState)state);
}
