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
using var session = engine.CreateSession(state, seed: 12345, start: SimTime.Zero);

while (session.CurrentTime.Tick <= 10)
    session.TickOnce();

session.RunUntil(new SimTime(500));
```

## Поведение

- `CurrentTime` указывает на следующий тик к выполнению.
- `RunTicks` реализован поверх сессии.
