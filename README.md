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
engine.Attach(run);
engine.RunTicks(state, seed: 1, endTickInclusive: 100);

Console.WriteLine(state.Population);

public sealed class WorldState
{
    public int Population { get; set; }
}
```

This is the fastest path: useful artifacts, deterministic run, almost zero wiring.

## Recommended Artifacts

`QuickRun`:
- Best for first use and basic debugging.
- Enables deterministic run metadata + snapshots + state fingerprints + trace.
- No event codec required.

```csharp
using var run = Epoch.QuickRun(state, rootDir: "artifacts");
```

`RecommendedRun`:
- Best for real debugging in apps and services.
- Enables fingerprints + snapshots + trace + profiling + failure artifacts.
- No codec required for this baseline.
- Add invariants when needed.

```csharp
using var run = Epoch.RecommendedRun(state, rootDir: "artifacts", invariants: null);
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

## RunTicks vs Session

Use `RunTicks` for batch jobs:

```csharp
engine.RunTicks(state, seed: 1, endTickInclusive: 1000);
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
