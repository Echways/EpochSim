Determinism Contract

EpochSim guarantees deterministic execution for a given set of inputs (initial state, seed, systems, commands/events, options). The following ordering and lifecycle rules are enforced:

1) Tick lifecycle
- OnRunStart is called exactly once before any ticks are processed.
- For each tick T, OnTickStart(T) is called before any event dispatch at tick T.
- OnTickEnd(T) is called after all command/event processing for tick T completes.
- OnRunEnd is always called once, even if the run fails.

2) System ordering
- Systems are ticked in the order they were registered.
- For each system, OnSystemTickStart(T, system) and OnSystemTickEnd(T, system) wrap the system’s Tick call for that tick.
- Event handling dispatches to systems in the same stable registration order.

3) Scheduler ordering
- Scheduled events are drained for the current tick only.
- For events scheduled to the same tick, dispatch order preserves insertion order (stable tie‑break by sequence).
- No scheduled events beyond endTickInclusive are dispatched.

4) Command/event pump
- Commands and events are processed FIFO.
- Within a tick, the engine repeats:
  a) Drain and dispatch all commands (FIFO), enqueueing emitted events.
  b) Drain and dispatch all events (FIFO) to all systems in stable order.
- The pump repeats until both queues are empty.

5) Middleware ordering
- Middleware callbacks are invoked in registration order for each hook.
- OnRunFailed is invoked exactly once when an exception occurs (before rethrow).

Any deviation from these rules is considered a correctness regression.
