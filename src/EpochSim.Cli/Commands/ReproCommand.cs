using System;
using System.Collections.Generic;
using EpochSim.Cli.App;
using EpochSim.Cli.Parsing;
using EpochSim.Execution;
using EpochSim.Execution.Middleware;
using EpochSim.Execution.RunArtifacts;
using EpochSim.Execution.Validation;
using EpochSim.Kernel.Time;
using EpochSim.Kernel.Validation;
using EpochSim.Samples.Population;
using EpochSim.Serialization.EventLog;
using EpochSim.Serialization.Snapshots;

namespace EpochSim.Cli.Commands
{
    public sealed class ReproCommand : ICliCommand
    {
        public int Execute(CommandContext ctx, string[] args)
        {
            var runArg = args.Length > 0 ? args[0] : "";

            var codec = (PopulationEventCodec)ctx.Codec;
            var stateSerializer = ctx.StateSerializer;

            var runId = CliParsing.ResolveRunIdWithEvents(ctx.Root, runArg);
            var paths = new RunPaths(ctx.Root, runId);

            var minRoot = Path.Combine(paths.RunDir, "minrepro");
            if (!Directory.Exists(minRoot))
                throw new DirectoryNotFoundException($"minrepro not found: {minRoot}");

            long? tick = null;
            if (args.Length > 1 && CliParsing.TryParseLong(args[1], out var t))
                tick = t;

            var minDir = tick.HasValue
                ? Path.Combine(minRoot, $"tick-{tick.Value}")
                : CliParsing.ResolveLatestMinRepro(minRoot);

            if (!Directory.Exists(minDir))
                throw new DirectoryNotFoundException($"minrepro dir not found: {minDir}");

            var metaPath = Path.Combine(minDir, "meta.txt");
            var mr = MinReproMetaReader.Read(metaPath);

            if (!MinReproMetaReader.TryGetLong(mr, "failureTick", out var failureTick))
                throw new InvalidOperationException($"minrepro meta missing failureTick: {metaPath}");

            if (!MinReproMetaReader.TryGetLong(mr, "snapshotTick", out var snapshotTick))
                snapshotTick = 0;

            ulong seed;
            if (args.Length > 2 && CliParsing.TryParseUlong(args[2], out var seedOverride))
                seed = seedOverride;
            else if (MinReproMetaReader.TryGetUlong(mr, "seed", out var seedFromMeta))
                seed = seedFromMeta;
            else
                seed = 12345UL;

            var snapPath = Path.Combine(minDir, "snapshot.json");
            var eventsPath = Path.Combine(minDir, "events.jsonl");

            if (!File.Exists(snapPath))
                throw new FileNotFoundException($"snapshot.json not found: {snapPath}");
            if (!File.Exists(eventsPath))
                throw new FileNotFoundException($"events.jsonl not found: {eventsPath}");

            var snap = SnapshotReader.Read(snapPath);
            var world = stateSerializer.Deserialize(snap.StateJson);

            var engine = new SimulationEngine<WorldState>();
            engine.AddSystem(new PopulationSystem());

            var memLog = new InMemoryEventLogMiddleware(codec);
            engine.AddMiddleware(memLog);

            var invariants = new List<IInvariant<WorldState>>
            {
                new PopulationNonNegativeInvariant(),
                new MaxEventsPerTickInvariant<WorldState>(() => memLog.EventsThisTick, maxEvents: 1000)
            };

            engine.AddMiddleware(new InvariantMiddleware<WorldState>(world, invariants, checkEveryTicks: 1));

            try
            {
                var entries = EventLogReader.ReadAll(eventsPath);
                var index = new EventLogIndex(entries);

                engine.ReplayFromLog(
                    state: world,
                    seed: seed,
                    start: new SimTime(snapshotTick + 1),
                    endInclusive: new SimTime(failureTick),
                    index: index,
                    codec: codec);

                Console.WriteLine($"RunDir={paths.RunDir}");
                Console.WriteLine($"RunId={paths.RunId}");
                Console.WriteLine($"MinReproDir={minDir}");
                Console.WriteLine("ReproFailed_NoViolation");
                return 4;
            }
            catch (InvariantViolationException ex)
            {
                Console.WriteLine($"RunDir={paths.RunDir}");
                Console.WriteLine($"RunId={paths.RunId}");
                Console.WriteLine($"MinReproDir={minDir}");
                Console.WriteLine(ex.Message);

                if (ex.Time.Tick == failureTick)
                {
                    Console.WriteLine("ReproOK");
                    return 0;
                }

                Console.WriteLine($"ReproMismatch_ExpectedTick={failureTick}_ActualTick={ex.Time.Tick}");
                return 3;
            }
        }
    }
}
