using System.Reflection;
using EpochSim.Execution;
using EpochSim.Kernel.Validation;
using EpochSim.Serialization.EventLog;
using EpochSim.Serialization.State;

namespace EpochSim;

public static class Epoch
{
    public static SimulationEngine<TState> CreateEngine<TState>()
        => new();

    public static JsonStateSerializer<TState> JsonStateSerializer<TState>()
        => new();

    public static IEventCodecV2 JsonCodecFromAssembly<TMarker>(
        Func<Type, bool>? filter = null,
        bool strictUnknownKinds = true)
        => JsonCodecFromAssembly(typeof(TMarker).Assembly, filter, strictUnknownKinds);

    public static IEventCodecV2 JsonCodecFromAssembly(
        Assembly assembly,
        Func<Type, bool>? filter = null,
        bool strictUnknownKinds = true)
    {
        var builder = new JsonEventCodecBuilder().RegisterFromAssembly(assembly, filter);
        if (strictUnknownKinds)
            builder.WithStrictUnknownKinds();

        return builder.Build();
    }

    public static EpochSimRunScope<TState> QuickRun<TState>(
        TState state,
        string? rootDir = null)
        => EpochSimRun.Quick(state, rootDir: rootDir);

    public static EpochSimRunScope<TState> RecommendedRun<TState>(
        TState state,
        string? rootDir = null,
        IEnumerable<IInvariant<TState>>? invariants = null)
        => EpochSimRun.Recommended(state, rootDir, invariants);

    public static EpochSimRunScope<TState> RecommendedRun<TState>(
        TState state,
        IEventCodecV2 codec,
        IStateSerializer<TState> serializer,
        string? rootDir = null,
        IEnumerable<IInvariant<TState>>? invariants = null)
        => EpochSimRun.Recommended(state, codec, serializer, rootDir, invariants);
}
