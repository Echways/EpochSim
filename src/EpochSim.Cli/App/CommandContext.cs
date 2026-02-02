using EpochSim.Samples.Population;
using EpochSim.Serialization.EventLog;
using EpochSim.Serialization.State;

namespace EpochSim.Cli.App;

public sealed record CommandContext(
    string Root,
    IEventCodec Codec,
    IStateSerializer<WorldState> StateSerializer);
