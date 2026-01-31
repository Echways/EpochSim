using EpochSim.Execution;
using EpochSim.Execution.Middleware;
using EpochSim.Execution.RunArtifacts;
using EpochSim.Execution.Snapshots;
using EpochSim.Execution.Validation;
using EpochSim.Kernel.Time;
using EpochSim.Kernel.Validation;
using EpochSim.Observability.Tracing;
using EpochSim.Samples.Population;
using EpochSim.Serialization.EventLog;
using EpochSim.Serialization.Snapshots;
using EpochSim.Serialization.State;

var cmd = args.Length > 0 ? args[0] : "run";
var root = args.Length > 1 ? args[1] : "artifacts";
var runArg = args.Length > 2 ? args[2] : "";
var endTick = args.Length > 3 && long.TryParse(args[3], out var et) ? et : 500;
var snapEvery = args.Length > 4 && long.TryParse(args[4], out var se) ? se : 50;
var seed = args.Length > 5 && ulong.TryParse(args[5], out var sd) ? sd : 12345UL;

var codec = new PopulationEventCodec();
var stateSerializer = new JsonStateSerializer<WorldState>();

if (cmd == "run")
{
    var runId = string.IsNullOrWhiteSpace(runArg) ? RunId.New() : NormalizeRunId(runArg);
    var paths = new RunPaths(root, runId);
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
}
else if (cmd == "validate-run")
{
    var runId = string.IsNullOrWhiteSpace(runArg) ? RunId.New() : NormalizeRunId(runArg);
    var paths = new RunPaths(root, runId);
    paths.Ensure();
    RunMetaWriter.Write(paths, mode: "validate-run", seed: seed, endTick: endTick, snapEvery: snapEvery);

    var engine = new SimulationEngine<WorldState>();
    engine.AddSystem(new PopulationSystem());
    engine.RegisterCommandHandler(new GrowPopulationHandler());
    engine.RegisterCommandHandler(new ScheduleFireHandler());

    var world = new WorldState();

    var memLog = new InMemoryEventLogMiddleware(codec);
    engine.AddMiddleware(memLog);

    var invariants = new List<EpochSim.Kernel.Validation.IInvariant<WorldState>>
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

    try
    {
        engine.RunTicks(world, seed: seed, start: SimTime.Zero, endInclusive: new SimTime(endTick));
        Console.WriteLine($"RunDir={paths.RunDir}");
        Console.WriteLine($"RunId={paths.RunId}");
        Console.WriteLine("ValidationOK");
        Console.WriteLine($"Population={world.Population}, Fires={world.Fires}");
    }
    catch (InvariantViolationException ex)
    {
        dumper.DumpOnViolation(ex);
        Console.WriteLine($"RunDir={paths.RunDir}");
        Console.WriteLine($"RunId={paths.RunId}");
        Console.WriteLine(ex.Message);
        Environment.ExitCode = 2;
    }
}
else if (cmd == "fast-replay")
{
    var runId = ResolveRunIdWithEvents(root, runArg);
    var paths = new RunPaths(root, runId);

    var engine = new SimulationEngine<WorldState>();
    engine.AddSystem(new PopulationSystem());

    var world = SnapshotRunner.LoadBestAndReplayTo(
        engine: engine,
        snapshotsDir: paths.SnapshotsDir,
        eventsPath: paths.EventsPath,
        serializer: stateSerializer,
        codec: codec,
        seed: seed,
        endTick: endTick,
        newState: () => new WorldState());

    Console.WriteLine($"RunDir={paths.RunDir}");
    Console.WriteLine($"RunId={paths.RunId}");
    Console.WriteLine($"Population={world.Population}, Fires={world.Fires}");
    Console.WriteLine($"EndTick={endTick}");
}
else if (cmd == "list-runs")
{
    var limit = args.Length > 2 && int.TryParse(args[2], out var l) ? l : 20;

    if (!Directory.Exists(root))
    {
        Console.WriteLine($"Artifacts root not found: {root}");
        Environment.ExitCode = 2;
    }
    else
    {
        var dirs = Directory.GetDirectories(root)
            .Select(d => new DirectoryInfo(d))
            .OrderByDescending(d => d.Name)
            .Take(Math.Max(1, limit))
            .ToArray();

        Console.WriteLine($"Root={root}");
        Console.WriteLine("RunId | HasMeta | HasEvents | Snapshots | Dumps | MinRepros");

        foreach (var d in dirs)
        {
            var runId = d.Name;
            var meta = File.Exists(Path.Combine(d.FullName, "meta.txt"));
            var events = File.Exists(Path.Combine(d.FullName, "events.jsonl"));
            var snaps = Directory.Exists(Path.Combine(d.FullName, "snapshots"))
                ? Directory.EnumerateFiles(Path.Combine(d.FullName, "snapshots"), "snapshot-*.json").Count()
                : 0;
            var dumps = Directory.Exists(Path.Combine(d.FullName, "dumps"))
                ? Directory.EnumerateFiles(Path.Combine(d.FullName, "dumps"), "violation-meta-*.txt").Count()
                : 0;
            var minrepros = Directory.Exists(Path.Combine(d.FullName, "minrepro"))
                ? Directory.GetDirectories(Path.Combine(d.FullName, "minrepro")).Length
                : 0;

            Console.WriteLine($"{runId} | {(meta ? "Y" : "N")} | {(events ? "Y" : "N")} | {snaps} | {dumps} | {minrepros}");
        }
    }
}
else if (cmd == "inspect-run")
{
    var runId = string.IsNullOrWhiteSpace(runArg) ? throw new ArgumentException("inspect-run требует runId или путь") : NormalizeRunId(runArg);
    var paths = new RunPaths(root, runId);

    if (!Directory.Exists(paths.RunDir))
    {
        Console.WriteLine($"RunDir not found: {paths.RunDir}");
        Environment.ExitCode = 2;
    }
    else
    {
        Console.WriteLine($"RunDir={paths.RunDir}");
        Console.WriteLine($"RunId={paths.RunId}");

        if (File.Exists(paths.MetaPath))
        {
            Console.WriteLine("Meta:");
            Console.WriteLine(File.ReadAllText(paths.MetaPath));
        }
        else
        {
            Console.WriteLine("Meta: missing");
        }

        if (File.Exists(paths.EventsPath))
        {
            var n = File.ReadLines(paths.EventsPath).LongCount();
            Console.WriteLine($"EventsLines={n}");
        }
        else
        {
            Console.WriteLine("Events: missing");
        }

        if (Directory.Exists(paths.SnapshotsDir))
        {
            var snaps = Directory.EnumerateFiles(paths.SnapshotsDir, "snapshot-*.json").ToArray();
            Console.WriteLine($"Snapshots={snaps.Length}");
            if (snaps.Length > 0)
            {
                var last = snaps.Select(p => new FileInfo(p)).OrderByDescending(f => f.Name).First();
                Console.WriteLine($"LastSnapshot={last.Name}");
            }
        }
        else
        {
            Console.WriteLine("Snapshots: missing");
        }

        if (Directory.Exists(paths.DumpsDir))
        {
            var dumps = Directory.EnumerateFiles(paths.DumpsDir, "violation-meta-*.txt").ToArray();
            Console.WriteLine($"DumpMetas={dumps.Length}");
            if (dumps.Length > 0)
            {
                var last = dumps.Select(p => new FileInfo(p)).OrderByDescending(f => f.Name).First();
                Console.WriteLine($"LastDumpMeta={last.Name}");
                Console.WriteLine("LastDumpMetaContent:");
                Console.WriteLine(File.ReadAllText(last.FullName));
            }
        }
        else
        {
            Console.WriteLine("Dumps: missing");
        }

        var minDir = Path.Combine(paths.RunDir, "minrepro");
        if (Directory.Exists(minDir))
        {
            var reps = Directory.GetDirectories(minDir).OrderByDescending(x => x).ToArray();
            Console.WriteLine($"MinRepros={reps.Length}");
            if (reps.Length > 0)
                Console.WriteLine($"LastMinRepro={reps[0]}");
        }
        else
        {
            Console.WriteLine("MinRepros: none");
        }
    }
}
else if (cmd == "bisect")
{
    var runId = ResolveRunIdWithEvents(root, runArg);
    var paths = new RunPaths(root, runId);

    if (!Directory.Exists(paths.RunDir))
        throw new DirectoryNotFoundException($"RunDir not found: {paths.RunDir}");

    var meta = RunMetaReader.Read(paths.MetaPath);

    if (args.Length <= 3 || !long.TryParse(args[3], out endTick))
    {
        if (!RunMetaReader.TryGetLong(meta, "endTick", out endTick))
            endTick = 500;
    }

    if (args.Length <= 5 || !ulong.TryParse(args[5], out seed))
    {
        if (!RunMetaReader.TryGetUlong(meta, "seed", out seed))
            seed = 12345UL;
    }

    if (!File.Exists(paths.EventsPath))
        throw new FileNotFoundException($"events.jsonl not found: {paths.EventsPath}");

    if (!Directory.Exists(paths.SnapshotsDir))
        Directory.CreateDirectory(paths.SnapshotsDir);

    bool FailsUpTo(long probeTick, out InvariantViolationException? ex)
    {
        var engine = new SimulationEngine<WorldState>();
        engine.AddSystem(new PopulationSystem());

        var memLog = new InMemoryEventLogMiddleware(codec);
        engine.AddMiddleware(memLog);

        var invariants = new List<EpochSim.Kernel.Validation.IInvariant<WorldState>>
        {
            new PopulationNonNegativeInvariant(),
            new MaxEventsPerTickInvariant<WorldState>(() => memLog.EventsThisTick, maxEvents: 1000)
        };

        var world = new WorldState();
        engine.AddMiddleware(new InvariantMiddleware<WorldState>(world, invariants, checkEveryTicks: 1));

        try
        {
            SnapshotRunner.LoadBestAndReplayTo(
                engine: engine,
                snapshotsDir: paths.SnapshotsDir,
                eventsPath: paths.EventsPath,
                serializer: stateSerializer,
                codec: codec,
                seed: seed,
                endTick: probeTick,
                newState: () => world);

            ex = null;
            return false;
        }
        catch (InvariantViolationException e)
        {
            ex = e;
            return true;
        }
    }

    Console.WriteLine($"RunDir={paths.RunDir}");
    Console.WriteLine($"RunId={paths.RunId}");
    Console.WriteLine($"EndTick={endTick}");
    Console.WriteLine($"Seed={seed}");

    if (!FailsUpTo(endTick, out var exAtEnd))
    {
        Console.WriteLine("NoInvariantViolationUpToEndTick");
        Environment.ExitCode = 0;
    }
    else
    {
        var hi = exAtEnd!.Time.Tick;
        var lo = 0L;

        while (lo < hi)
        {
            var mid = lo + ((hi - lo) / 2);

            if (FailsUpTo(mid, out var exMid))
            {
                hi = exMid!.Time.Tick;
            }
            else
            {
                lo = mid + 1;
            }
        }

        FailsUpTo(lo, out var exFinal);

        Console.WriteLine($"FirstViolationTick={lo}");
        if (exFinal is not null)
        {
            Console.WriteLine($"Invariant={exFinal.InvariantName}");
            Console.WriteLine($"Message={exFinal.Detail}");

            var minDir = MinReproWriter.Create(
                paths: paths,
                failureTick: lo,
                seed: seed,
                endTick: endTick,
                invariantName: exFinal.InvariantName,
                invariantMessage: exFinal.Detail,
                serializer: stateSerializer,
                newState: () => new WorldState());

            Console.WriteLine($"MinReproDir={minDir}");
        }

        Environment.ExitCode = 2;
    }
}
else if (cmd == "repro")
{
    var runId = ResolveRunIdWithEvents(root, runArg);
    var paths = new RunPaths(root, runId);

    var minRoot = Path.Combine(paths.RunDir, "minrepro");
    if (!Directory.Exists(minRoot))
        throw new DirectoryNotFoundException($"minrepro not found: {minRoot}");

    long? tick = null;
    if (args.Length > 3 && long.TryParse(args[3], out var t))
        tick = t;

    var minDir = tick.HasValue
        ? Path.Combine(minRoot, $"tick-{tick.Value}")
        : ResolveLatestMinRepro(minRoot);

    if (!Directory.Exists(minDir))
        throw new DirectoryNotFoundException($"minrepro dir not found: {minDir}");

    var metaPath = Path.Combine(minDir, "meta.txt");
    var mr = MinReproMetaReader.Read(metaPath);

    if (!MinReproMetaReader.TryGetLong(mr, "failureTick", out var failureTick))
        throw new InvalidOperationException($"minrepro meta missing failureTick: {metaPath}");

    if (!MinReproMetaReader.TryGetLong(mr, "snapshotTick", out var snapshotTick))
        snapshotTick = 0;

    if (args.Length > 4 && ulong.TryParse(args[4], out var seedOverride))
        seed = seedOverride;
    else if (MinReproMetaReader.TryGetUlong(mr, "seed", out var seedFromMeta))
        seed = seedFromMeta;

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

    var invariants = new List<EpochSim.Kernel.Validation.IInvariant<WorldState>>
    {
        new PopulationNonNegativeInvariant(),
        new MaxEventsPerTickInvariant<WorldState>(() => memLog.EventsThisTick, maxEvents: 1000)
    };

    engine.AddMiddleware(new InvariantMiddleware<WorldState>(world, invariants, checkEveryTicks: 1));

    try
    {
        var entries = EventLogReader.ReadAll(eventsPath);
        var index = new EventLogIndex(entries);

        engine.ReplayFromLog(world, seed, new SimTime(snapshotTick + 1), new SimTime(failureTick), index, codec);

        Console.WriteLine($"RunDir={paths.RunDir}");
        Console.WriteLine($"RunId={paths.RunId}");
        Console.WriteLine($"MinReproDir={minDir}");
        Console.WriteLine("ReproFailed_NoViolation");
        Environment.ExitCode = 4;
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
            Environment.ExitCode = 0;
        }
        else
        {
            Console.WriteLine($"ReproMismatch_ExpectedTick={failureTick}_ActualTick={ex.Time.Tick}");
            Environment.ExitCode = 3;
        }
    }
}
else
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  run <artifactsRoot> [runId] [endTick] [snapEvery] [seed]");
    Console.WriteLine("  validate-run <artifactsRoot> [runId] [endTick] [snapEvery] [seed]");
    Console.WriteLine("  fast-replay <artifactsRoot> [runIdOrRunDirOrEmptyForLatest] [endTick] [ignored] [seed]");
    Console.WriteLine("  list-runs <artifactsRoot> [limit]");
    Console.WriteLine("  inspect-run <artifactsRoot> <runIdOrRunDir>");
    Console.WriteLine("  bisect <artifactsRoot> [runIdOrRunDirOrEmptyForLatestWithEvents] [endTick] [ignored] [seed]");
    Console.WriteLine("  repro <artifactsRoot> [runIdOrEmptyForLatestWithEvents] [failureTickOrEmptyForLatest] [seedOverride]");
}

static string NormalizeRunId(string s)
{
    s = s.Trim();
    s = s.TrimEnd('/', '\\');
    if (s.Contains(Path.DirectorySeparatorChar) || s.Contains(Path.AltDirectorySeparatorChar))
        return Path.GetFileName(s);
    return s;
}

static string ResolveRunIdWithEvents(string root, string runArg)
{
    runArg = runArg.Trim();

    if (!string.IsNullOrWhiteSpace(runArg))
    {
        var id = NormalizeRunId(runArg);
        var dir = Path.Combine(root, id);
        var events = Path.Combine(dir, "events.jsonl");
        if (!File.Exists(events))
            throw new FileNotFoundException($"events.jsonl not found for run: {dir}");
        return id;
    }

    if (!Directory.Exists(root))
        throw new DirectoryNotFoundException($"Artifacts root not found: {root}");

    var dirs = Directory.GetDirectories(root)
        .Select(d => new DirectoryInfo(d))
        .OrderByDescending(d => d.Name);

    foreach (var d in dirs)
    {
        var events = Path.Combine(d.FullName, "events.jsonl");
        if (File.Exists(events))
            return d.Name;
    }

    throw new InvalidOperationException($"No runs with events.jsonl found in {root}");
}

static string ResolveLatestMinRepro(string minRoot)
{
    var dirs = Directory.GetDirectories(minRoot)
        .Select(d => new DirectoryInfo(d))
        .OrderByDescending(d => d.Name)
        .ToArray();

    if (dirs.Length == 0)
        throw new InvalidOperationException($"No minrepro dirs found in {minRoot}");

    return dirs[0].FullName;
}
