# Embedding EpochSim

Use `EpochSim.All` as the primary meta-package for embedding.
The run builder API is in the `EpochSim.Hosting` namespace.

Install:

```bash
dotnet add package EpochSim.All
```

## Core workflow

1. Configure `SimulationEngine<TState>` (systems + command handlers).
2. Build `IEventCodecV2` with `JsonEventCodecBuilder`.
3. Build `EpochSimRunScope<TState>` with required middleware.
4. Attach scope to engine and run.
5. Dispose scope to flush writers and finalize run metadata.

## Minimal example

```csharp
var state = new WorldState();
var engine = new SimulationEngine<WorldState>();

var serializer = new JsonStateSerializer<WorldState>();
var codec = new JsonEventCodecBuilder()
    .Register<MyEvent>()
    .Build();

using var run = EpochSimRun.For(state)
    .WithRootDirectory("artifacts")
    .WithRunId(RunId.New())
    .WithEventLog(codec)
    .WithTraceJsonl()
    .WithStateFingerprints(serializer)
    .Build();

engine.Attach(run);
engine.RunTicks(state, seed: 12345, start: SimTime.Zero, endInclusive: new SimTime(100), context: run.Context);
```

## Related docs

- Artifact naming and files: `docs/Artifacts.md`
- Long-lived session API: `docs/Sessions.md`
- Determinism rules and pitfalls: `docs/Determinism.md`
