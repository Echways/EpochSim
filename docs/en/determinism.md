# Determinism

EpochSim guarantees deterministic execution for identical inputs: initial state, seed, systems, commands and events, and options. Systems and handlers must not introduce other sources of variation.

## Common pitfalls

- `DateTime.Now`, `DateTime.UtcNow`
- `Random` instead of `ctx.Rng`
- `Guid.NewGuid()` in simulation logic
- iteration order of hash collections
- thread races that change event order
- uncontrolled floating-point behavior

## Recommended practices

- Use only `ctx.Rng` for domain randomness.
- Keep command and event handlers explicit, with controlled side effects.
- Keep `statefp.jsonl` to compare runs.
- Enable snapshots for fast deterministic replay.

## Rules

Any deviation from these rules is a correctness regression.

### 1. Run lifecycle

- `OnRunStart` is called exactly once before tick processing starts.
- `OnRunEnd` is called exactly once, including on failure.
- On an exception, `OnRunFailed` is called once before the exception is rethrown.

### 2. Tick order

- For each tick `T`, `OnTickStart(T)` is called first.
- No events for tick `T` are dispatched before `OnTickStart(T)`.
- `OnTickEnd(T)` is called after command and event processing completes.

### 3. System order

- Systems tick in registration order.
- `OnSystemTickStart(T, system)` and `OnSystemTickEnd(T, system)` wrap each `Tick` call.
- Event handling (`Handle`) also runs in system registration order.

### 4. Scheduler

- During tick `T`, only events scheduled for `T` are processed.
- Events scheduled for the same tick are processed in insertion order.
- Events beyond `endTickInclusive` are not dispatched.

### 5. Command and event pump

Within each tick the engine repeats until both queues are empty:

1. Drain and process all commands in FIFO order, adding events to the queue.
2. Drain and process all events in FIFO order, for all systems in registration order.

### 6. Middleware

- Middleware callbacks run in registration order for each hook.
- The sequence `OnRunStart`, ticks, `OnRunEnd` is stable.
- `OnRunFailed` is called exactly once.
