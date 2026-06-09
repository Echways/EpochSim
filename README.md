# EpochSim

Deterministic tick simulation engine for .NET 10.

Recommended package for embedding:

```bash
dotnet add package EpochSim
```

`EpochSim.All` is useful when you want one dependency that brings the full module graph (`EpochSim`, `Execution`, `Serialization`, `Observability`, `Kernel`):

```bash
dotnet add package EpochSim.All
```

Russian version: [README.ru.md](README.ru.md)

## State-Only Quickstart (No Events, No Codecs)

```csharp
using EpochSim;

var state = new WorldState();
var engine = Epoch.CreateEngine<WorldState>();
engine.AddSystem("World", tick: ctx => ctx.State.Population++);

using var run = Epoch.QuickRun(state, rootDir: "artifacts");
run.RunTicks(engine, seed: 1, endTickInclusive: 100);

Console.WriteLine(state.Population);

public sealed class WorldState
{
    public int Population { get; set; }
}
```

> **Note**: always call `run.RunTicks(engine, ...)` — not `engine.RunTicks(state, ...)` — to avoid
> silently passing a different state instance than the one the run scope was built with.

This is the fastest path: useful artifacts, deterministic run, almost zero wiring.

## Preset Cost Table

| Preset | Fingerprint cadence | Mutation guard | Typical use |
|---|---|---|---|
| `QuickRun` | every tick | ✗ | First use, CI smoke test |
| `RecommendedRun` | every 50 ticks | ✗ | Production debugging |
| `DebugRun` | every tick | ✓ | Invariant hunting, strict mode |

**Mutation guard** serialises state before and after each system tick and throws
`InvalidOperationException` if the fingerprint changes. Useful for finding systems that
mutate state outside of event handlers, but has significant serialisation overhead — keep
it in `DebugRun` only.

```csharp
// QuickRun — zero-config, no codec needed
using var run = Epoch.QuickRun(state, rootDir: "artifacts");
run.RunTicks(engine, seed: 1, endTickInclusive: 100);

// RecommendedRun — adds profiling + failure artifacts
using var run = Epoch.RecommendedRun(state, rootDir: "artifacts");
run.RunTicks(engine, seed: 1, endTickInclusive: 1000);

// DebugRun — every-tick fingerprinting + mutation guard
var serializer = Epoch.JsonStateSerializer<WorldState>();
using var run = Epoch.DebugRun(state, serializer, rootDir: "artifacts");
run.RunTicks(engine, seed: 1, endTickInclusive: 200);
```

Advanced overloads stay available when you want full control:
- `Epoch.RecommendedRun(state, codec, serializer, rootDir, invariants)`
- `EpochSimRun.For(state)...Build()`

## Event Log / Replay (Use When You Need Replay/Bisect)

Event log is optional and intended for replay workflows (`verify-run`, `bisect`, `fast-replay`).

```csharp
using EpochSim;
using EpochSim.Kernel.Messaging;

var codec = Epoch.JsonCodecFromAssembly<Program>(t => t == typeof(Changed));
var serializer = Epoch.JsonStateSerializer<WorldState>();

using var run = Epoch.RecommendedRun(state, codec, serializer, rootDir: "artifacts");
```

```csharp
[MessageKind("Changed")]
public sealed record Changed(int Delta) : IEvent;
```

## Reading Artifacts In-Process

After a run completes you can read artifacts directly from the run paths without going
through the CLI:

```csharp
using EpochSim;
using EpochSim.Execution.RunArtifacts;
using EpochSim.Execution.StateFingerprint;
using EpochSim.Serialization.EventLog;    // EventLogReader, EventLogEntryV2
using EpochSim.Serialization.State;

var state = new WorldState();
var serializer = Epoch.JsonStateSerializer<WorldState>();
var codec = Epoch.JsonCodecFromAssembly<Program>(t => t == typeof(Changed));
var engine = Epoch.CreateEngine<WorldState>();
engine.AddSystem("World", tick: ctx => ctx.State.Population++);

string runDir;
using (var run = Epoch.RecommendedRun(state, codec, serializer, rootDir: "artifacts"))
{
    run.RunTicks(engine, seed: 1, endTickInclusive: 100);
    runDir = run.Paths.RunDir;
}

// Read the manifest written by Dispose()
var manifest = RunManifestReader.TryRead(Path.Combine(runDir, "manifest.json"));
Console.WriteLine($"EndTick={manifest?.EndTick}");

// Read per-tick fingerprints
var fingerprints = JsonlStateFingerprintWriter.ReadAll(Path.Combine(runDir, "statefp.jsonl"));
Console.WriteLine($"Fingerprints recorded: {fingerprints.Count}");

// Read event log entries
var entries = EventLogReader.ReadAll(Path.Combine(runDir, "events.jsonl"));
Console.WriteLine($"Events logged: {entries.Count}");
```

## RunTicks vs Session

Use `RunTicks` for batch jobs:

```csharp
run.RunTicks(engine, seed: 1, endTickInclusive: 1000);
```

Use `SimulationSession<TState>` for long-lived loops (games, servers, interactive stepping):

```csharp
var inbox = new CommandInbox();
engine.AttachInbox(inbox);

using var session = engine.CreateSession(state, seed: 1, startTick: 0);
inbox.Enqueue(new AddPopulation(5));
session.TickOnce();
session.RunUntil(100);
```

```csharp
using EpochSim.Kernel.Messaging;
[MessageKind("AddPopulation")]
public sealed record AddPopulation(int Delta) : ICommand;
```

## External Input Pattern (HTTP/UI/Queue -> Sim)

Use inboxes to feed external commands deterministically into the sim thread:

- `CommandInbox`: commands for the next tick.
- `ScheduledCommandInbox`: commands scheduled at a specific tick with stable ordering:
  - primary key: tick ascending
  - secondary key: insertion order

```csharp
var scheduled = new ScheduledCommandInbox();
engine.AttachScheduledInbox(scheduled);

scheduled.Enqueue(10, new AddPopulation(3));
scheduled.Enqueue(10, new AddPopulation(7));
scheduled.Enqueue(12, new AddPopulation(1));
```

See full integration recipes: [Embedding](docs/Embedding.md)

## CLI Onboarding

Create a starter app:

```bash
dotnet run --project src/EpochSim.Cli -- init .
```

Run and inspect artifacts:

```bash
dotnet run --project src/EpochSim.Cli -- run artifacts
dotnet run --project src/EpochSim.Cli -- list-runs artifacts
dotnet run --project src/EpochSim.Cli -- inspect-run artifacts <runId>
```

Determinism debugging:

```bash
dotnet run --project src/EpochSim.Cli -- verify-run artifacts <runId>
dotnet run --project src/EpochSim.Cli -- bisect artifacts <runId>
```

## Samples

- Recommended simple sample: [QuickstartSample](src/EpochSim.Samples/Quickstart/QuickstartSample.cs)
- Advanced/legacy domain sample: [Event-heavy diagnostic domain](src/EpochSim.Samples/Population/)

## Build and Test

```bash
dotnet build EpochSim.slnx -m:1
dotnet test EpochSim.slnx -m:1
```

## Docs

English:
- [Embedding](docs/Embedding.md)
- [Artifacts](docs/Artifacts.md)
- [Sessions](docs/Sessions.md)
- [Determinism](docs/Determinism.md)

Russian:
- [README.ru.md](README.ru.md)
- [Embedding.ru](docs/Embedding.ru.md)
- [Artifacts.ru](docs/Artifacts.ru.md)
- [Sessions.ru](docs/Sessions.ru.md)
- [Determinism.ru](docs/Determinism.ru.md)
