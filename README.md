# EpochSim

Deterministic tick simulation engine for .NET 10.

`EpochSim` is the main facade package: minimal wiring, fast onboarding, and clean APIs for embedding.

Russian version: [README.ru.md](README.ru.md)

## Install

```bash
dotnet add package EpochSim
```

If you want the full module graph in one dependency, install:

```bash
dotnet add package EpochSim.All
```

## 60-Second Quickstart

```csharp
using EpochSim;
using EpochSim.Kernel.Messaging;

var state = new WorldState();
var engine = Epoch.CreateEngine<WorldState>();

engine.AddSystem(
    "World",
    tick: ctx => ctx.Commands.Enqueue(new Grow(1)),
    handle: (ctx, ev) =>
    {
        if (ev is Grew e)
            ctx.State.Population += e.Delta;
    });

engine.OnCommand<Grow>((_, cmd, events) => events.Emit(new Grew(cmd.Delta)));

var codec = Epoch.JsonCodecFromAssembly<Program>(t => t == typeof(Grew));
var serializer = Epoch.JsonStateSerializer<WorldState>();

using var run = Epoch.RecommendedRun(state, codec, serializer, rootDir: "artifacts");
engine.Attach(run);
engine.RunTicks(state, seed: 12345, endTickInclusive: 100);

Console.WriteLine($"RunId={run.RunId}");
Console.WriteLine($"RunDir={run.Paths.RunDir}");

public sealed class WorldState
{
    public int Population { get; set; }
}

[MessageKind("Grow")]
public sealed record Grow(int Delta) : ICommand;
public sealed record Grew(int Delta) : IEvent;
```

Result: one deterministic run under `artifacts/<runId>/` with standard artifacts.

## What You Get Out Of The Box

- Minimal domain boilerplate:
  - `IEvent`/`ICommand` do not require manual `Kind`.
  - `ISystem<TState>` has default `Name` and no-op `Handle`.
- Lambda registration:
  - `engine.AddSystem(name, tick, handle?)`
  - `engine.OnCommand<TCommand>((state, cmd, events) => ...)`
- Simple run overloads:
  - `RunTicks(state, seed, endTickInclusive)`
  - `RunTicks(state, seed, startTick, endTickInclusive)`
- Presets for artifacts:
  - `EpochSimRun.Quick(...)`
  - `EpochSimRun.Recommended(...)`

## Artifact Presets

- Quick:
  - `EpochSimRun.Quick(state, codec?, serializer?, rootDir?)`
  - Good for fast smoke runs.
- Recommended:
  - `EpochSimRun.Recommended(state, codec, serializer, rootDir?, invariants?)`
  - Enables compression, event log, snapshots, fingerprints, trace, profiling, mutation guard, failure artifacts.

## Long-Lived Session API

```csharp
using EpochSim.Kernel.Time;

using var session = engine.CreateSession(state, seed: 12345, start: SimTime.Zero);

while (session.CurrentTime.Tick <= 10)
    session.TickOnce();

session.RunUntil(new SimTime(500));
```

Session surface:
- `SimTime CurrentTime { get; }`
- `bool TickOnce(CancellationToken ct = default)`
- `void RunUntil(SimTime endInclusive, CancellationToken ct = default)`

## Advanced Builder (Explicit Wiring)

```csharp
using EpochSim;
using EpochSim.Execution.RunArtifacts;
using EpochSim.Kernel.Validation;

var serializer = Epoch.JsonStateSerializer<WorldState>();
var codec = Epoch.JsonCodecFromAssembly<Program>(t => t == typeof(Grew));
IInvariant<WorldState>[] invariants = [];

using var run = EpochSimRun.For(state)
    .WithRootDirectory("artifacts")
    .WithRunId(RunId.New())
    .WithRecommendedDefaults(codec, serializer, invariants)
    .Build();

engine.Attach(run);
engine.RunTicks(state, seed: 12345, endTickInclusive: 100);
```

## CLI Quickstart

```bash
dotnet run --project src/EpochSim.Cli -- init .
dotnet run --project src/EpochSim.Cli -- run artifacts
dotnet run --project src/EpochSim.Cli -- list-runs artifacts
```

## Determinism Checklist

Avoid these in systems and handlers:
- `DateTime.Now` / `DateTime.UtcNow`
- `Guid.NewGuid()`
- custom `Random` instances (use `ctx.Rng`)
- relying on unordered collection iteration
- hidden multithreading races

## Build and Test

```bash
dotnet build EpochSim.slnx -m:1
dotnet test EpochSim.slnx -m:1
```

## Documentation

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
