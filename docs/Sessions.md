# Sessions

`SimulationSession<TState>` is for step-by-step and long-lived simulation loops.

## API

- `SimTime CurrentTime { get; }`
- `bool TickOnce(CancellationToken ct = default)`
- `void RunUntil(SimTime endInclusive, CancellationToken ct = default)`
- `Dispose()`

## Example

```csharp
using var session = engine.CreateSession(state, seed: 12345, start: SimTime.Zero);

while (session.CurrentTime.Tick <= 10)
    session.TickOnce();

session.RunUntil(new SimTime(500));
```

## Behavior

- `CurrentTime` points to the next tick to execute.
- `RunTicks` is implemented on top of session internals.
