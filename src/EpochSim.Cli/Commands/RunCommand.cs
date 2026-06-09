using System;
using System.Threading;
using EpochSim.Cli.App;
using EpochSim.Cli.Domain;
using EpochSim.Cli.Parsing;
using EpochSim.Execution;
using EpochSim.Execution.RunArtifacts;
using EpochSim;
using EpochSim.Kernel.Determinism;
using EpochSim.Kernel.Time;

namespace EpochSim.Cli.Commands;

public sealed class RunCommand : DomainCommandBase
{
    protected override int Execute<TState>(IDomainAdapter<TState> adapter, CommandContext ctx, string[] args)
    {
        var positional = new List<string>();
        var guardState = false;
        var compress = false;
        long? snapshotEveryOpt = null;
        long? fingerprintEveryOpt = null;
        int? maxPumpStepsOpt = null;
        int? maxEventsOpt = null;
        int? cancelAfterMsOpt = null;
        RngVersion? rngVersionOpt = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (!CliParsing.IsOption(arg))
            {
                positional.Add(arg);
                continue;
            }

            switch (arg.ToLowerInvariant())
            {
                case "--guard-state":
                    guardState = true;
                    break;
                case "--compress":
                    compress = true;
                    break;
                case "--snapshot-every":
                    if (TryReadLong(args, ref i, out var snapshotEvery)) snapshotEveryOpt = snapshotEvery;
                    break;
                case "--fingerprint-every":
                    if (TryReadLong(args, ref i, out var fingerprintEveryArg)) fingerprintEveryOpt = fingerprintEveryArg;
                    break;
                case "--max-pump-steps":
                    if (TryReadInt(args, ref i, out var maxPumpSteps)) maxPumpStepsOpt = maxPumpSteps;
                    break;
                case "--max-events-per-tick":
                    if (TryReadInt(args, ref i, out var maxEvents)) maxEventsOpt = maxEvents;
                    break;
                case "--cancel-after-ms":
                    if (TryReadInt(args, ref i, out var cancelAfterMs)) cancelAfterMsOpt = cancelAfterMs;
                    break;
                case "--rng-version":
                    if (TryReadRngVersion(args, ref i, out var rngVersion)) rngVersionOpt = rngVersion;
                    break;
            }
        }

        var runArg = positional.Count > 0 ? positional[0] : "";
        var endTick = positional.Count > 1 && CliParsing.TryParseLong(positional[1], out var et) ? et : 500;
        var snapEvery = snapshotEveryOpt ?? (positional.Count > 2 && CliParsing.TryParseLong(positional[2], out var se) ? se : 50);
        var seed = positional.Count > 3 && CliParsing.TryParseUlong(positional[3], out var sd) ? sd : 12345UL;
        var fingerprintEvery = fingerprintEveryOpt ?? 1;
        if (fingerprintEvery <= 0) fingerprintEvery = 1;

        var codec = adapter.Codec;
        var stateSerializer = adapter.Serializer;

        var runId = string.IsNullOrWhiteSpace(runArg) ? RunId.New() : CliParsing.NormalizeRunId(runArg);

        var engine = new SimulationEngine<TState>();
        adapter.ConfigureEngine(engine);

        var simulationState = adapter.CreateInitialState();

        var runBuilder = EpochSimRun.For(simulationState)
            .WithRootDirectory(ctx.Root)
            .WithRunId(runId)
            .WithCompression(compress)
            .WithEventLog(codec)
            .WithTraceJsonl()
            .WithStateFingerprints(stateSerializer, fingerprintEvery)
            .WithFailureArtifacts(stateSerializer, codec, tailSize: 200);

        if (snapEvery > 0)
            runBuilder.WithSnapshots(stateSerializer, snapEvery);

        if (guardState)
            runBuilder.WithStateMutationGuard(stateSerializer);

        using var run = runBuilder.Build();

        var defaultOptions = new RunOptions();
        var options = new RunOptions
        {
            MaxPumpStepsPerTick = maxPumpStepsOpt ?? defaultOptions.MaxPumpStepsPerTick,
            MaxEventsPerTick = maxEventsOpt ?? defaultOptions.MaxEventsPerTick,
            RngVersion = rngVersionOpt ?? defaultOptions.RngVersion
        };

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(ctx.Cancellation);
        if (cancelAfterMsOpt.HasValue && cancelAfterMsOpt.Value > 0)
            cancellation.CancelAfter(cancelAfterMsOpt.Value);

        run.RunTicks(
            engine,
            seed: seed,
            start: SimTime.Zero,
            endInclusive: new SimTime(endTick),
            options: options,
            cancellationToken: cancellation.Token);

        Console.WriteLine($"RunDir={run.Paths.RunDir}");
        Console.WriteLine($"RunId={run.RunId}");
        foreach (var line in adapter.DescribeState(simulationState))
            Console.WriteLine(line);

        return 0;
    }

    private static bool TryReadLong(string[] args, ref int index, out long value)
    {
        value = default;
        if (index + 1 >= args.Length)
            return false;

        index++;
        return CliParsing.TryParseLong(args[index], out value);
    }

    private static bool TryReadInt(string[] args, ref int index, out int value)
    {
        value = default;
        if (index + 1 >= args.Length)
            return false;

        index++;
        return CliParsing.TryParseInt(args[index], out value);
    }

    private static bool TryReadRngVersion(string[] args, ref int index, out RngVersion version)
    {
        version = default;
        if (index + 1 >= args.Length)
            return false;

        index++;
        var raw = args[index];
        if (string.Equals(raw, "v1", StringComparison.OrdinalIgnoreCase))
        {
            version = RngVersion.V1;
            return true;
        }

        if (string.Equals(raw, "v2", StringComparison.OrdinalIgnoreCase))
        {
            version = RngVersion.V2;
            return true;
        }

        return false;
    }
}
