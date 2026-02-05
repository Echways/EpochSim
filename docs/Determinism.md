# Determinism

Avoid non-deterministic sources inside systems and handlers.

## Common pitfalls

- `DateTime.Now` / `DateTime.UtcNow`
- `Random` instead of `ctx.Rng`
- `Guid.NewGuid()` in simulation logic
- relying on hash-collection iteration order
- hidden thread races affecting event order
- uncontrolled floating-point behavior

## Recommended practices

- use only `ctx.Rng` for domain randomness
- keep command/event handlers explicit and side-effect controlled
- persist `statefp.jsonl` for run-to-run comparison
- enable snapshots for fast deterministic replay

## Determinism Contract

EpochSim guarantees deterministic execution for identical inputs (initial state, seed, systems, commands/events, options). The rules below are part of the behavioral contract.

### 1) Run lifecycle

- `OnRunStart` is called exactly once before tick processing starts.
- `OnRunEnd` is always called exactly once, even on failures.
- If an exception is thrown, `OnRunFailed` is called once before rethrow.

### 2) Tick order

- For each tick `T`, `OnTickStart(T)` is called first.
- No events for tick `T` are dispatched before `OnTickStart(T)`.
- `OnTickEnd(T)` is called after command/event processing is complete.

### 3) System order

- Systems tick in registration order.
- `OnSystemTickStart(T, system)` / `OnSystemTickEnd(T, system)` wrap each `Tick` call.
- Event handling (`Handle`) also runs in system registration order.

### 4) Scheduler rules

- During tick `T`, only events scheduled for `T` are processed.
- Events scheduled for the same tick are processed in insertion order (stable queue).
- Events beyond `endTickInclusive` are not dispatched.

### 5) Command/event pump loop

Within each tick, the engine repeats until both queues are empty:

1. Drain and process all commands (FIFO), adding events to the queue.
2. Drain and process all events (FIFO) for all systems in registration order.

### 6) Middleware order

- Middleware callbacks are invoked in middleware registration order for each hook.
- The sequence `OnRunStart` -> ticks -> `OnRunEnd` is stable.
- `OnRunFailed` is called exactly once.

Any deviation from this contract is considered a correctness regression.
