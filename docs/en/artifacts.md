# Artifacts

`RunPaths` is the single source of artifact naming. Every run writes to its own directory:

```
artifacts/<runId>/
```

## Files

| File                                    | Content                    |
| --------------------------------------- | -------------------------- |
| `events.jsonl`, `events.jsonl.gz`       | Event log                  |
| `trace.jsonl`, `trace.jsonl.gz`         | Trace stream               |
| `profile.jsonl`, `profile.jsonl.gz`     | Profiling stream           |
| `statefp.jsonl`                         | State fingerprints         |
| `manifest.json`                         | Run manifest               |
| `meta.txt`                              | Compact run metadata       |

The `.gz` variants are written when compression is enabled.

## Directories

| Directory    | Content                                      |
| ------------ | -------------------------------------------- |
| `snapshots/` | Periodic state snapshots                     |
| `dumps/`     | Invariant violation and fail-fast dumps      |

## Failure artifacts

Written when a run fails:

- `failure-report.json`
- `failure-snapshot.json`, when snapshot output is enabled

## Finalization

`manifest.json` and `meta.txt` are finalized in `EpochSimRunScope<TState>.Dispose()`. Dispose the run scope before reading them.

## Reading artifacts from code

Artifacts are plain files in the run directory and can be read in-process. The event log is optional and required for `verify-run`, `bisect` and `fast-replay`:

```csharp
using EpochSim;
using EpochSim.Execution.RunArtifacts;
using EpochSim.Execution.StateFingerprint;
using EpochSim.Kernel.Messaging;
using EpochSim.Serialization.EventLog;

var state = new WorldState();
var engine = Epoch.CreateEngine<WorldState>();
var inbox = new ScheduledCommandInbox();
engine.AttachScheduledInbox(inbox);
engine.OnCommand<AddPopulation>((world, command, events) =>
{
    world.Population += command.Delta;
    events.Emit(new Changed(command.Delta));
});
inbox.Enqueue(5, new AddPopulation(3));

var codec = Epoch.JsonCodecFromAssembly<Program>(type => type == typeof(Changed));
var serializer = Epoch.JsonStateSerializer<WorldState>();

string runDir;
using (var run = Epoch.RecommendedRun(state, codec, serializer, rootDir: "artifacts"))
{
    run.RunTicks(engine, seed: 1, endTickInclusive: 100);
    runDir = run.Paths.RunDir;
}

var manifest = RunManifestReader.TryRead(Path.Combine(runDir, "manifest.json"));
var fingerprints = JsonlStateFingerprintWriter.ReadAll(Path.Combine(runDir, "statefp.jsonl"));
var entries = EventLogReader.ReadAll(Path.Combine(runDir, "events.jsonl"));

Console.WriteLine($"EndTick={manifest?.EndTick}, fingerprints={fingerprints.Count}, events={entries.Count}");

[MessageKind("AddPopulation")]
public sealed record AddPopulation(int Delta) : ICommand;

[MessageKind("Changed")]
public sealed record Changed(int Delta) : IEvent;

public sealed class WorldState
{
    public int Population { get; set; }
}
```
