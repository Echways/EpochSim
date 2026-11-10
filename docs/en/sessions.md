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
using EpochSim;
using EpochSim.Kernel.Time;

var state = new WorldState();
var engine = Epoch.CreateEngine<WorldState>();
engine.AddSystem("World", tick: ctx => ctx.State.Population++);

using var session = engine.CreateSession(state, seed: 12345, start: SimTime.Zero);

while (session.CurrentTime.Tick <= 10)
    session.TickOnce();

session.RunUntil(new SimTime(500));

public sealed class WorldState
{
    public int Population { get; set; }
}
```

## Behavior

- `CurrentTime` points to the next tick to execute.
- `RunTicks` is implemented on top of the session.
