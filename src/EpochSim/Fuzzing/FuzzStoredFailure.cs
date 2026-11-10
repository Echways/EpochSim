using System.Text.Json.Serialization;

namespace EpochSim.Fuzzing;

public sealed record FuzzStoredFailure(
    string Name,
    string StateType,
    ulong Seed,
    FuzzFailureKind Kind,
    string Identity,
    long Tick,
    long ReproTicks,
    string Signature,
    IReadOnlyList<int>? CommandIndices,
    IReadOnlyList<ulong> OtherSeeds,
    DateTimeOffset FirstSeen,
    bool Fixed)
{
    [JsonIgnore]
    public IEnumerable<ulong> AllSeeds => OtherSeeds.Prepend(Seed);
}
