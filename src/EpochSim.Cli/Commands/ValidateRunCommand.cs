using System;
using System.Collections.Generic;
using System.Threading;
using EpochSim.Cli.App;
using EpochSim.Cli.Domain;
using EpochSim.Cli.Parsing;
using EpochSim.Execution;
using EpochSim.Execution.Middleware;
using EpochSim;
using EpochSim.Kernel.Determinism;
using EpochSim.Kernel.Time;
using EpochSim.Kernel.Validation;
using EpochRunId = EpochSim.Execution.RunArtifacts.RunId;

namespace EpochSim.Cli.Commands;

public sealed class ValidateRunCommand : DomainCommandBase
{
    public string Help =>
        """
        validate-run <artifactsRoot> [runId] [--end-tick N] [--seed N] [--guard-state] [--compress]
            [--fingerprint-every N] [--snapshot-every N] [--max-pump-steps N]
            [--max-events-per-tick N] [--rng-version v1|v2] [--cancel-after-ms N]

        Run the simulation with invariant checking and write artifacts to <artifactsRoot>.

          --end-tick N           Last tick to simulate (default: 500)
          --seed N               RNG seed (default: 12345)
          --guard-state          Enable mutation guard (throws if system mutates state during tick)
          --compress             Write gzip-compressed event log
          --snapshot-every N     Snapshot state every N ticks (default: 50)
          --fingerprint-every N  Record fingerprint every N ticks (default: 1)
          --max-pump-steps N     Max pump iterations per tick
          --max-events-per-tick N  Max events dispatched per tick
          --rng-version v1|v2    RNG algorithm version (default: v2)
          --cancel-after-ms N    Abort after N milliseconds
        """;

    protected override int Execute<TState>(IDomainAdapter<TState> adapter, CommandContext ctx, string[] args)
    {
        var positional = new List<string>();
        var guardState = false;
        var compress = false;
        long? endTickOpt = null;
        ulong? seedOpt = null;
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
                case "--end-tick":
                    if (TryReadLong(args, ref i, out var endTickArg)) endTickOpt = endTickArg;
                    break;
                case "--seed":
                    if (TryReadUlong(args, ref i, out var seedArg)) seedOpt = seedArg;
                    break;
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
        var endTick = endTickOpt ?? (positional.Count > 1 && CliParsing.TryParseLong(positional[1], out var et) ? et : 500);
        var snapEvery = snapshotEveryOpt ?? (positional.Count > 2 && CliParsing.TryParseLong(positional[2], out var se) ? se : 50);
        var seed = seedOpt ?? (positional.Count > 3 && CliParsing.TryParseUlong(positional[3], out var sd) ? sd : 12345UL);
        var fingerprintEvery = fingerprintEveryOpt ?? 1;
        if (fingerprintEvery <= 0) fingerprintEvery = 1;

        var codec = adapter.Codec;
        var stateSerializer = adapter.Serializer;

        var runId = string.IsNullOrWhiteSpace(runArg) ? EpochRunId.New() : CliParsing.NormalizeRunId(runArg);

        var engine = new SimulationEngine<TState>();
        adapter.ConfigureEngine(engine);

        var simulationState = adapter.CreateInitialState();

        var memLog = new InMemoryEventLogMiddleware(codec);
        engine.AddMiddleware(memLog);

        var invariants = new List<IInvariant<TState>>();
        invariants.AddRange(adapter.CreateInvariants());
        invariants.Add(new MaxEventsPerTickInvariant<TState>(() => memLog.EventsThisTick, maxEvents: 1000));

        var runBuilder = EpochSimRun.For(simulationState)
            .WithRootDirectory(ctx.Root)
            .WithRunId(runId)
            .WithCompression(compress)
            .WithEventLog(codec)
            .WithStateFingerprints(stateSerializer, fingerprintEvery)
            .WithFailureArtifacts(stateSerializer, codec, tailSize: 200)
            .WithInvariants(invariants, checkEveryTicks: 1);

        if (snapEvery > 0)
            runBuilder.WithSnapshots(stateSerializer, snapEvery);

        if (guardState)
            runBuilder.WithStateMutationGuard(stateSerializer);

        using var run = runBuilder.Build();

        var dumper = new FailFastDumpMiddleware<TState>(
            state: simulationState,
            currentTickProvider: () => memLog.CurrentTick,
            eventLogProvider: () => memLog.Entries,
            serializer: stateSerializer,
            dumpDirectory: run.Paths.DumpsDir);

        var defaultOptions = new RunOptions();
        var options = new RunOptions
        {
            MaxPumpStepsPerTick = maxPumpStepsOpt ?? defaultOptions.MaxPumpStepsPerTick,
            MaxEventsPerTick = maxEventsOpt ?? defaultOptions.MaxEventsPerTick,
            RngVersion = rngVersionOpt ?? defaultOptions.RngVersion
        };

        try
        {
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
            Console.WriteLine("ValidationOK");
            foreach (var line in adapter.DescribeState(simulationState))
                Console.WriteLine(line);
            return 0;
        }
        catch (InvariantViolationException ex)
        {
            dumper.DumpOnViolation(ex);
            Console.WriteLine($"RunDir={run.Paths.RunDir}");
            Console.WriteLine($"RunId={run.RunId}");
            Console.WriteLine(ex.Message);
            return 2;
        }
    }

    private static bool TryReadLong(string[] args, ref int index, out long value)
    {
        value = default;
        if (index + 1 >= args.Length)
            return false;

        index++;
        return CliParsing.TryParseLong(args[index], out value);
    }

    private static bool TryReadUlong(string[] args, ref int index, out ulong value)
    {
        value = default;
        if (index + 1 >= args.Length)
            return false;

        index++;
        return CliParsing.TryParseUlong(args[index], out value);
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
