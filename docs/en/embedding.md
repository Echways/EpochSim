# Embedding

Integrating EpochSim into an application: execution model, external input and diagnostics.

## Batch runs and sessions

`RunTicks` executes a bounded batch run:

```csharp
engine.RunTicks(state, seed: 1, endTickInclusive: 10_000);
```

`CreateSession` serves long-lived applications such as servers, games and interactive tools:

```csharp
using var session = engine.CreateSession(state, seed: 1, startTick: 0);
session.TickOnce();
session.RunUntil(1_000);
```

A session keeps random number generator and scheduler continuity across `TickOnce` calls. See [Sessions](sessions.md).

## State and persistence

- Runtime state is an in-memory `TState`, mutated deterministically by systems and handlers.
- Persistent state is optional and produced as run artifacts: `statefp.jsonl` for fingerprints and `snapshots/` for point-in-time state.
- `manifest.json` and `meta.txt` describe the run settings and lifecycle.

Minimal integration needs no codec and no events:

```csharp
var state = new WorldState();
var engine = Epoch.CreateEngine<WorldState>();
engine.AddSystem("World", tick: ctx => ctx.State.Population++);

using var run = Epoch.QuickRun(state, rootDir: "artifacts");
run.RunTicks(engine, seed: 1, endTickInclusive: 100);
```

Run through `run.RunTicks(engine, ...)` so the state is the one the run scope was built with. A different instance attached to the same engine fails with a state mismatch error.

## External input

1. Producers such as HTTP endpoints, queues and UI enqueue commands.
2. The simulation thread drains the inbox at tick boundaries.
3. The engine processes commands inside its deterministic pump.

### Single-thread loop

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

### Server with scheduled input

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

`ScheduledCommandInbox` order is deterministic: tick ascending, then insertion order within a tick.

## Choosing a preset

| Preset           | Use                                           |
| ---------------- | --------------------------------------------- |
| `QuickRun`       | Local integration and smoke checks            |
| `RecommendedRun` | Baseline for services and production diagnostics |
| `DebugRun`       | Per-tick fingerprints and mutation guard      |

`RecommendedRun` enables:

- compressed artifacts
- state fingerprints every 50 ticks
- snapshots every 50 ticks
- trace and profiling streams
- failure artifacts on crashes
- the event log, when a codec is supplied
- invariant checks every tick, when invariants are supplied

Enable the event log only when replay or bisect is needed. For explicit control over the codec, run identifier and individual artifacts use `Epoch.RecommendedRun(state, codec, serializer, ...)` or `EpochSimRun.For(state)`.

## Debugging determinism

Run a simulation:

```bash
epochsim run artifacts
```

Verify fingerprints against the recorded run:

```bash
epochsim verify-run artifacts <runId>
```

Find the first tick where runs diverge:

```bash
epochsim bisect artifacts <runId>
```

Replay from the nearest snapshot and the event log:

```bash
epochsim fast-replay artifacts <runId>
```

## Related

- [Artifacts](artifacts.md)
- [Sessions](sessions.md)
- [Determinism](determinism.md)
