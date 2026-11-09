using EpochSim.Execution;
using EpochSim.Kernel.Validation;
using EpochSim.Serialization.EventLog;
using EpochSim.Serialization.State;

namespace EpochSim.Fuzzing;

internal sealed record FuzzSettings<TState>(
    Func<TState> StateFactory,
    Action<SimulationEngine<TState>> ConfigureEngine,
    long EndTick,
    IEnumerable<ulong> Seeds,
    IStateSerializer<TState> Serializer,
    Func<IEnumerable<IInvariant<TState>>>? Invariants,
    Action<FuzzCommandPlan>? Commands,
    bool CheckDeterminism,
    int Parallelism,
    bool StopAtFirstFailure,
    string? ArtifactsRoot,
    IEventCodecV2? EventCodec,
    IProgress<FuzzProgress>? Progress);
