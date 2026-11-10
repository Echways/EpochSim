# Сессии

`SimulationSession<TState>` выполняет симуляцию пошагово и подходит для долгоживущих циклов.

## API

| Член                                                                   | Описание                              |
| ---------------------------------------------------------------------- | ------------------------------------- |
| `SimTime CurrentTime { get; }`                                         | Следующий тик к выполнению            |
| `bool TickOnce(CancellationToken ct = default)`                        | Выполнить один тик                    |
| `void RunUntil(SimTime endInclusive, CancellationToken ct = default)`  | Выполнять до тика включительно        |
| `void RunUntil(long endTickInclusive, CancellationToken ct = default)` | То же, по номеру тика                 |
| `Dispose()`                                                            | Освободить сессию                     |

## Пример

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

## Поведение

- `CurrentTime` указывает на следующий тик к выполнению.
- `RunTicks` реализован поверх сессии.
