# Embedding EpochSim

This guide is about application integration, not low-level builder details.

## RunTicks vs Session

Use `RunTicks` when you want a bounded batch run:

```csharp
engine.RunTicks(state, seed: 1, endTickInclusive: 10_000);
```

Use `CreateSession` when your app is long-lived (server/game/interactive):

```csharp
using var session = engine.CreateSession(state, seed: 1, startTick: 0);
session.TickOnce();
session.RunUntil(1_000);
```

`Session` keeps RNG and scheduler continuity across many `TickOnce` calls.

## Where State Lives

- Runtime state is in memory (`TState`) and is mutated deterministically by your systems/handlers.
- Persistent state is optional and produced via artifacts:
  - `statefp.jsonl` for lightweight fingerprints.
  - `snapshots/` for point-in-time state persistence.
- `manifest.json` + `meta.txt` describe run settings and lifecycle.

Quick integration (no codecs/events needed):

```csharp
var state = new WorldState();
var engine = Epoch.CreateEngine<WorldState>();
engine.AddSystem("World", tick: ctx => ctx.State.Population++);

using var run = Epoch.QuickRun(state, rootDir: "artifacts");
engine.Attach(run);
engine.RunTicks(state, seed: 1, endTickInclusive: 100);
```

## External Inputs and Command Flow

Pattern:
1. Producers (HTTP endpoints, queues, UI) enqueue commands.
2. Simulation thread drains inbox at tick boundaries.
3. Commands are processed in the deterministic engine pump.

### Recipe: Single-Thread App Loop

```csharp
using EpochSim;
using EpochSim.Kernel.Messaging;

var state = new WorldState();
var engine = Epoch.CreateEngine<WorldState>();
var inbox = new CommandInbox();

engine.AttachInbox(inbox);
engine.OnCommand<AddPopulation>((s, cmd, _) => s.Population += cmd.Delta);

using var run = Epoch.QuickRun(state, "artifacts");
engine.Attach(run);

using var session = engine.CreateSession(state, seed: 1, startTick: 0);
inbox.Enqueue(new AddPopulation(5));
session.TickOnce();
```

### Recipe: Server Pattern with Scheduled Inputs

```csharp
using EpochSim;
using EpochSim.Kernel.Messaging;

var state = new WorldState();
var engine = Epoch.CreateEngine<WorldState>();
var scheduledInbox = new ScheduledCommandInbox();

engine.AttachScheduledInbox(scheduledInbox);
engine.OnCommand<AddPopulation>((s, cmd, _) => s.Population += cmd.Delta);

using var session = engine.CreateSession(state, seed: 1, startTick: 0);

scheduledInbox.Enqueue(10, new AddPopulation(2));
scheduledInbox.Enqueue(10, new AddPopulation(3));
scheduledInbox.Enqueue(11, new AddPopulation(1));

session.RunUntil(20);
```

`ScheduledCommandInbox` order is deterministic:
- tick ascending
- insertion order within the same tick

## Artifacts to Enable in Production

Default advice:
- `QuickRun`: local integration and smoke checks.
- `RecommendedRun`: service debugging baseline.

`RecommendedRun` gives:
- fingerprints (every tick)
- snapshots (periodic)
- trace + profiling streams
- failure artifacts on crashes

Enable event log only when you need replay/bisect workflows.

## Debugging Determinism with CLI

Run a simulation:

```bash
dotnet run --project src/EpochSim.Cli -- run artifacts
```

Verify fingerprint equivalence:

```bash
dotnet run --project src/EpochSim.Cli -- verify-run artifacts <runId>
```

Find the first mismatch tick:

```bash
dotnet run --project src/EpochSim.Cli -- bisect artifacts <runId>
```

Replay from best snapshot + events:

```bash
dotnet run --project src/EpochSim.Cli -- fast-replay artifacts <runId>
```

## Advanced Wiring

Use `Epoch.RecommendedRun(state, codec, serializer, ...)` or `EpochSimRun.For(state)` when you need explicit event codec control, custom run IDs, and per-artifact toggles.

Related docs:
- [Artifacts](Artifacts.md)
- [Sessions](Sessions.md)
- [Determinism](Determinism.md)
