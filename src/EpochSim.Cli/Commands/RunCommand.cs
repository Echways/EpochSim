using System;
using EpochSim.Cli.App;
using EpochSim.Cli.Parsing;
using EpochSim.Execution;
using EpochSim.Execution.Middleware;
using EpochSim.Execution.RunArtifacts;
using EpochSim.Execution.Snapshots;
using EpochSim.Execution.StateFingerprint;
using EpochSim.Kernel.Time;
using EpochSim.Observability.Tracing;
using EpochSim.Samples.Population;
using EpochSim.Serialization.EventLog;

namespace EpochSim.Cli.Commands
{
    public sealed class RunCommand : ICliCommand
    {
        public int Execute(CommandContext ctx, string[] args)
        {
            var runArg = args.Length > 0 ? args[0] : "";
            var endTick = args.Length > 1 && CliParsing.TryParseLong(args[1], out var et) ? et : 500;
            var snapEvery = args.Length > 2 && CliParsing.TryParseLong(args[2], out var se) ? se : 50;
            var seed = args.Length > 3 && CliParsing.TryParseUlong(args[3], out var sd) ? sd : 12345UL;

            var codec = (PopulationEventCodec)ctx.Codec;
            var stateSerializer = ctx.StateSerializer;

            var runId = string.IsNullOrWhiteSpace(runArg) ? RunId.New() : CliParsing.NormalizeRunId(runArg);
            var paths = new RunPaths(ctx.Root, runId);
            paths.Ensure();
            RunMetaWriter.Write(paths, mode: "run", seed: seed, endTick: endTick, snapEvery: snapEvery);

            var engine = new SimulationEngine<WorldState>();
            engine.AddSystem(new PopulationSystem());
            engine.RegisterCommandHandler(new GrowPopulationHandler());
            engine.RegisterCommandHandler(new ScheduleFireHandler());

            var traceSink = new InMemoryTraceSink();
            engine.AddMiddleware(new TraceMiddleware(traceSink));

            using var eventWriter = new EventLogWriter(paths.EventsPath);
            engine.AddMiddleware(new EventLogMiddleware(eventWriter, codec));

            var world = new WorldState();

            using var fpWriter = new JsonlStateFingerprintWriter(paths.StateFpPath);
            engine.AddMiddleware(new StateFingerprintMiddleware<WorldState>(world, stateSerializer, fpWriter));

            engine.AddMiddleware(new SnapshotMiddleware<WorldState>(world, snapEvery, paths.SnapshotsDir, stateSerializer));

            engine.RunTicks(world, seed: seed, start: SimTime.Zero, endInclusive: new SimTime(endTick));

            using (var writer = new JsonlTraceWriter(paths.TracePath))
            {
                foreach (var r in traceSink.Records)
                    writer.Write(r);
            }

            Console.WriteLine($"RunDir={paths.RunDir}");
            Console.WriteLine($"RunId={paths.RunId}");
            Console.WriteLine($"Population={world.Population}, Fires={world.Fires}");
            Console.WriteLine($"TraceFingerprint={TraceFingerprint.Compute(traceSink.Records)}");

            return 0;
        }
    }
}
