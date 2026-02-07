using EpochSim.Kernel.Validation;
using EpochSim.Serialization.EventLog;
using EpochSim.Serialization.State;

namespace EpochSim;

public static class EpochSimRun
{
    public static EpochSimRunBuilder<TState> For<TState>(TState state)
        => new(state);

    public static EpochSimRunScope<TState> Quick<TState>(
        TState state,
        IEventCodecV2? codec = null,
        IStateSerializer<TState>? serializer = null,
        string? rootDir = null)
    {
        var builder = For(state);

        if (!string.IsNullOrWhiteSpace(rootDir))
            builder.WithRootDirectory(rootDir);

        builder.WithCompression(true).WithTraceJsonl();

        if (codec is not null)
            builder.WithEventLog(codec);

        if (serializer is not null)
        {
            builder.WithStateFingerprints(serializer, everyTicks: 1);

            if (codec is not null)
                builder.WithFailureArtifacts(serializer, codec, tailSize: 200);
        }

        return builder.Build();
    }

    public static EpochSimRunScope<TState> Recommended<TState>(
        TState state,
        IEventCodecV2 codec,
        IStateSerializer<TState> serializer,
        string? rootDir = null,
        IEnumerable<IInvariant<TState>>? invariants = null)
    {
        var builder = For(state);

        if (!string.IsNullOrWhiteSpace(rootDir))
            builder.WithRootDirectory(rootDir);

        return builder.BuildRecommended(codec, serializer, invariants);
    }
}
