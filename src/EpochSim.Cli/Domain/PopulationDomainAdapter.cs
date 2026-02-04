using System.Linq;
using EpochSim.Execution;
using EpochSim.Execution.Diagnostics;
using EpochSim.Kernel.Systems;
using EpochSim.Kernel.Validation;
using EpochSim.Samples.Population;
using EpochSim.Serialization.EventLog;
using EpochSim.Serialization.State;

namespace EpochSim.Cli.Domain;

public sealed class PopulationDomainAdapter : IDomainAdapter<WorldState>
{
    public string Name => "population";
    public Type StateType => typeof(WorldState);
    public IEventCodecV2 Codec { get; } = new PopulationEventCodec();
    public IStateSerializer<WorldState> Serializer { get; } = new JsonStateSerializer<WorldState>();
    public IEventPayloadFormatter PayloadFormatter { get; } =
        new CompositeEventPayloadFormatter(new PopulationEventPayloadFormatter(), new JsonEventPayloadFormatter());

    object IDomainAdapter.Serializer => Serializer;

    public WorldState CreateInitialState() => new();

    object IDomainAdapter.CreateInitialState() => CreateInitialState();

    public void ConfigureEngine(SimulationEngine<WorldState> engine)
    {
        engine.AddSystem(new PopulationSystem());
        engine.RegisterCommandHandler(new GrowPopulationHandler());
        engine.RegisterCommandHandler(new ScheduleFireHandler());
    }

    void IDomainAdapter.ConfigureEngine(object engine)
        => ConfigureEngine((SimulationEngine<WorldState>)engine);

    public IReadOnlyList<IInvariant<WorldState>> CreateInvariants()
        => new List<IInvariant<WorldState>> { new PopulationNonNegativeInvariant() };

    IReadOnlyList<object> IDomainAdapter.CreateInvariants()
        => CreateInvariants().Cast<object>().ToList();

    public IReadOnlyList<string> DescribeState(WorldState state)
        => new List<string>
        {
            $"Population={state.Population}",
            $"Fires={state.Fires}"
        };

    IReadOnlyList<string> IDomainAdapter.DescribeState(object state)
        => DescribeState((WorldState)state);
}
