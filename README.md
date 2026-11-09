<div align="center">

# EpochSim

**A deterministic tick simulation engine for .NET 10.**
Run a simulation from a seed, record fingerprints and events, then verify, replay and bisect any run.

**English** · [Русский](README.ru.md)

[![NuGet](https://img.shields.io/nuget/v/EpochSim)](https://www.nuget.org/packages/EpochSim)
[![CI](https://github.com/Echways/EpochSim/actions/workflows/ci.yml/badge.svg)](https://github.com/Echways/EpochSim/actions/workflows/ci.yml)
[![License](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

</div>

## Why EpochSim

A simulation that cannot be reproduced cannot be debugged. EpochSim fixes the inputs of every tick, so the same seed and the same commands always produce the same state, and a divergence can be located to a single tick.

- **Deterministic by construction.** A run is defined by its state, seed and ordered input. Systems execute in a fixed order on one simulation thread.
- **Run presets.** `QuickRun`, `RecommendedRun` and `DebugRun` cover smoke tests, production diagnostics and strict invariant hunting with one call.
- **Artifacts for every run.** Manifest, per-tick state fingerprints and an optional event log are written to disk and readable from code.
- **Replay and bisect.** Verify a run against its recorded fingerprints and find the first tick where two runs diverge.
- **Deterministic input.** `CommandInbox` and `ScheduledCommandInbox` feed commands from HTTP, UI or queues in a stable order.
- **Long-lived sessions.** `SimulationSession<TState>` supports games, servers and interactive stepping.
- **A CLI for the whole workflow.** Run, list, inspect, validate, replay, bisect and reproduce runs from the terminal.

## Installation

```bash
dotnet add package EpochSim
```

Requires .NET 10. `EpochSim.All` brings the full module graph (`EpochSim`, `Execution`, `Serialization`, `Observability`, `Kernel`) as a single dependency:

```bash
dotnet add package EpochSim.All
```

The command-line tool is installed separately:

```bash
dotnet tool install --global EpochSim.Cli
```

## Quick start

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

Always call `run.RunTicks(engine, ...)` rather than `engine.RunTicks(state, ...)`, so the state passed in is the one the run scope was built with.

More in [Embedding](docs/en/embedding.md).

## Run presets

| Preset           | Fingerprint cadence | Mutation guard | Typical use                    |
| ---------------- | ------------------- | -------------- | ------------------------------ |
| `QuickRun`       | every tick          | No             | First use, CI smoke test       |
| `RecommendedRun` | every 50 ticks      | No             | Production diagnostics         |
| `DebugRun`       | every tick          | Yes            | Invariant hunting, strict mode |

The mutation guard serializes state before and after each system and throws `InvalidOperationException` when the fingerprint changes. It finds systems that mutate state outside event handlers, at a significant serialization cost: use it in `DebugRun` only.

```csharp
using var quick = Epoch.QuickRun(state, rootDir: "artifacts");
quick.RunTicks(engine, seed: 1, endTickInclusive: 100);

using var recommended = Epoch.RecommendedRun(state, rootDir: "artifacts");
recommended.RunTicks(engine, seed: 1, endTickInclusive: 1000);

var serializer = Epoch.JsonStateSerializer<WorldState>();
using var debug = Epoch.DebugRun(state, serializer, rootDir: "artifacts");
debug.RunTicks(engine, seed: 1, endTickInclusive: 200);
```

For full control use `Epoch.RecommendedRun(state, codec, serializer, rootDir, invariants)` or `EpochSimRun.For(state)...Build()`.

## Event log and replay

The event log is optional and required for `verify-run`, `bisect` and `fast-replay`.

```csharp
using EpochSim;
using EpochSim.Kernel.Messaging;

var codec = Epoch.JsonCodecFromAssembly<Program>(t => t == typeof(Changed));
var serializer = Epoch.JsonStateSerializer<WorldState>();

using var run = Epoch.RecommendedRun(state, codec, serializer, rootDir: "artifacts");

[MessageKind("Changed")]
public sealed record Changed(int Delta) : IEvent;
```

## Reading artifacts

Artifacts are plain files in the run directory and can be read in-process:

```csharp
using EpochSim;
using EpochSim.Execution.RunArtifacts;
using EpochSim.Execution.StateFingerprint;
using EpochSim.Serialization.EventLog;

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
```

Layout and formats are described in [Artifacts](docs/en/artifacts.md).

## Sessions and external input

`RunTicks` fits batch jobs. `SimulationSession<TState>` fits long-lived loops:

```csharp
var inbox = new CommandInbox();
engine.AttachInbox(inbox);

using var session = engine.CreateSession(state, seed: 1, startTick: 0);
inbox.Enqueue(new AddPopulation(5));
session.TickOnce();
session.RunUntil(100);

[MessageKind("AddPopulation")]
public sealed record AddPopulation(int Delta) : ICommand;
```

Inboxes feed external commands into the simulation thread deterministically:

- `CommandInbox`: commands for the next tick.
- `ScheduledCommandInbox`: commands for a specific tick, ordered by tick ascending, then by insertion order.

```csharp
var scheduled = new ScheduledCommandInbox();
engine.AttachScheduledInbox(scheduled);

scheduled.Enqueue(10, new AddPopulation(3));
scheduled.Enqueue(10, new AddPopulation(7));
scheduled.Enqueue(12, new AddPopulation(1));
```

More in [Sessions](docs/en/sessions.md).

## Command line

```bash
epochsim init .
epochsim run artifacts
epochsim list-runs artifacts
epochsim inspect-run artifacts <runId>
```

Determinism debugging:

```bash
epochsim verify-run artifacts <runId>
epochsim bisect artifacts <runId>
```

| Command                                            | Purpose                                    |
| -------------------------------------------------- | ------------------------------------------ |
| `init`, `run`                                      | Create a starter app, run it               |
| `list-runs`, `inspect-run`, `pretty-inspect`       | Browse and inspect recorded runs           |
| `validate-run`, `verify-run`                       | Check artifacts and fingerprints           |
| `fast-replay`, `bisect`, `repro`                   | Replay a run, find the first divergence    |
| `event-stats`, `timeline`, `list-adapters`         | Analyze events and list available adapters |

## What is included

| Area          | Highlights                                                                    |
| ------------- | ----------------------------------------------------------------------------- |
| Engine        | Ordered systems, seeded runs, tick and session execution.                     |
| Presets       | `QuickRun`, `RecommendedRun`, `DebugRun`, builder for custom setups.          |
| Input         | `CommandInbox`, `ScheduledCommandInbox` with stable ordering.                 |
| Artifacts     | Manifest, state fingerprints, event log, failure artifacts, profiling.        |
| Serialization | JSON state serializer and message codec discovered from an assembly.          |
| Diagnostics   | Mutation guard, invariants, replay verification, bisect.                      |
| CLI           | `epochsim` .NET tool covering the whole run lifecycle.                        |

## Documentation

|                                   |                                                      |
| --------------------------------- | ---------------------------------------------------- |
| [Embedding](docs/en/embedding.md)    | Integration recipes: hosts, servers, external input  |
| [Artifacts](docs/en/artifacts.md)    | Run directory layout and file formats                |
| [Sessions](docs/en/sessions.md)      | Long-lived loops and stepping                        |
| [Determinism](docs/en/determinism.md) | Rules that keep runs reproducible                   |

## Samples

[QuickstartSample](src/EpochSim.Samples/Quickstart/QuickstartSample.cs) is the minimal starting point. [Population](src/EpochSim.Samples/Population/) is an event-heavy domain with invariants and replay.

## Build and test

```bash
dotnet build EpochSim.slnx -m:1
dotnet test EpochSim.slnx -m:1
```

## License

[MIT](LICENSE)
