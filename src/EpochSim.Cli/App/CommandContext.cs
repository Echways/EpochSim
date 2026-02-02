using EpochSim.Samples.Population;
using EpochSim.Serialization.State;

namespace EpochSim.Cli.App;

public sealed record CommandContext(
    string Root,
    object Codec,
    JsonStateSerializer<WorldState> StateSerializer);