using System;
using System.Collections.Generic;
using EpochSim.Cli.App;
using EpochSim.Cli.Parsing;
using EpochSim.Execution;
using EpochSim.Execution.Middleware;
using EpochSim.Execution.RunArtifacts;
using EpochSim.Execution.StateFingerprint;
using EpochSim.Kernel.Time;
using EpochSim.Kernel.Validation;
using EpochSim.Samples.Population;
using EpochSim.Serialization.EventLog;

namespace EpochSim.Cli.Commands;

public sealed class ValidateRunCommand : ICliCommand
{
    public int Execute(CommandContext ctx, string[] args)
    {
        var positional = new List<string>();
        var guardState = false;
        var compress = false;
        long? snapshotEveryOpt = null;
        long? fingerprintEveryOpt = null;
        int? maxPumpStepsOpt = null;
        int? maxEventsOpt = null;

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
            }
        }

        var runArg = positional.Count > 0 ? positional[0] : "";
        var endTick = positional.Count > 1 && CliParsing.TryParseLong(positional[1], out var et) ? et : 500;
        var snapEvery = snapshotEveryOpt ?? (positional.Count > 2 && CliParsing.TryParseLong(positional[2], out var se) ? se : 50);
        var seed = positional.Count > 3 && CliParsing.TryParseUlong(positional[3], out var sd) ? sd : 12345UL;
        var fingerprintEvery = fingerprintEveryOpt ?? 1;
        if (fingerprintEvery <= 0) fingerprintEvery = 1;

        var codec = ctx.Codec;
        var stateSerializer = ctx.StateSerializer;

        var runId = string.IsNullOrWhiteSpace(runArg) ? RunId.New() : CliParsing.NormalizeRunId(runArg);
        var paths = new RunPaths(ctx.Root, runId);
        paths.Ensure();
        RunMetaWriter.Write(paths, mode: "validate-run", seed: seed, endTick: endTick, snapEvery: snapEvery, fingerprintEvery: fingerprintEvery);

        var engine = new SimulationEngine<WorldState>();
        engine.AddSystem(new PopulationSystem());
        engine.RegisterCommandHandler(new GrowPopulationHandler());
        engine.RegisterCommandHandler(new ScheduleFireHandler());

        var world = new WorldState();

        var memLog = new InMemoryEventLogMiddleware(codec);
        engine.AddMiddleware(memLog);

        var eventsPath = compress ? paths.EventsPathGz : paths.EventsPath;
        using var eventWriter = new EventLogWriter(eventsPath);
        engine.AddMiddleware(new EventLogMiddleware(eventWriter, codec));

        using var fpWriter = new JsonlStateFingerprintWriter(paths.StateFpPath);
        engine.AddMiddleware(new StateFingerprintMiddleware<WorldState>(world, stateSerializer, fpWriter, fingerprintEvery));

        if (guardState)
            engine.AddMiddleware(new StateMutationGuardMiddleware<WorldState>(world, stateSerializer));

        engine.AddMiddleware(new SnapshotMiddleware<WorldState>(world, snapEvery, paths.SnapshotsDir, stateSerializer));

        var invariants = new List<IInvariant<WorldState>>
        {
            new PopulationNonNegativeInvariant(),
            new MaxEventsPerTickInvariant<WorldState>(() => memLog.EventsThisTick, maxEvents: 1000)
        };

        engine.AddMiddleware(new InvariantMiddleware<WorldState>(world, invariants, checkEveryTicks: 1));

        var dumper = new FailFastDumpMiddleware<WorldState>(
            state: world,
            currentTickProvider: () => memLog.CurrentTick,
            eventLogProvider: () => memLog.Entries,
            serializer: stateSerializer,
            dumpDirectory: paths.DumpsDir);

        var defaults = new RunOptions();
        var options = new RunOptions
        {
            MaxPumpStepsPerTick = maxPumpStepsOpt ?? defaults.MaxPumpStepsPerTick,
            MaxEventsPerTick = maxEventsOpt ?? defaults.MaxEventsPerTick
        };

        var manifest = new RunManifest(
            EngineVersion: RunManifestWriter.GetEngineVersion(),
            Seed: seed,
            StartTick: 0,
            EndTick: endTick,
            EventLogVersion: 2,
            SnapshotEvery: snapEvery,
            FingerprintEvery: fingerprintEvery,
            MaxPumpStepsPerTick: options.MaxPumpStepsPerTick,
            MaxEventsPerTick: options.MaxEventsPerTick,
            StrictReplay: false);

        RunManifestWriter.Write(paths.ManifestPath, manifest);

        try
        {
            engine.RunTicks(world, seed: seed, start: SimTime.Zero, endInclusive: new SimTime(endTick), options: options);
            Console.WriteLine($"RunDir={paths.RunDir}");
            Console.WriteLine($"RunId={paths.RunId}");
            Console.WriteLine("ValidationOK");
            Console.WriteLine($"Population={world.Population}, Fires={world.Fires}");
            return 0;
        }
        catch (InvariantViolationException ex)
        {
            dumper.DumpOnViolation(ex);
            Console.WriteLine($"RunDir={paths.RunDir}");
            Console.WriteLine($"RunId={paths.RunId}");
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

    private static bool TryReadInt(string[] args, ref int index, out int value)
    {
        value = default;
        if (index + 1 >= args.Length)
            return false;

        index++;
        return CliParsing.TryParseInt(args[index], out value);
    }
}
