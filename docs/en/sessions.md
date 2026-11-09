# Sessions

`SimulationSession<TState>` runs a simulation step by step and suits long-lived loops.

## API

| Member                                                           | Description                        |
| ---------------------------------------------------------------- | ---------------------------------- |
| `SimTime CurrentTime { get; }`                                   | Next tick to execute               |
| `bool TickOnce(CancellationToken ct = default)`                  | Execute one tick                   |
| `void RunUntil(SimTime endInclusive, CancellationToken ct = default)` | Execute up to and including the tick |
| `void RunUntil(long endTickInclusive, CancellationToken ct = default)` | Same, from a tick number           |
| `Dispose()`                                                      | Release the session                |

## Example

```csharp
using var session = engine.CreateSession(state, seed: 12345, start: SimTime.Zero);

while (session.CurrentTime.Tick <= 10)
    session.TickOnce();

session.RunUntil(new SimTime(500));
```

## Behavior

- `CurrentTime` points to the next tick to execute.
- `RunTicks` is implemented on top of the session.
