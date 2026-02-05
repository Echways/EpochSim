# Сессии

`SimulationSession<TState>` нужен для пошагового и долгоживущего выполнения симуляции.

## API

- `SimTime CurrentTime { get; }`
- `bool TickOnce(CancellationToken ct = default)`
- `void RunUntil(SimTime endInclusive, CancellationToken ct = default)`
- `Dispose()`

## Пример

```csharp
using var session = engine.CreateSession(state, seed: 12345, start: SimTime.Zero);

while (session.CurrentTime.Tick <= 10)
    session.TickOnce();

session.RunUntil(new SimTime(500));
```

## Поведение

- `CurrentTime` указывает на следующий тик к выполнению.
- `RunTicks` реализован поверх внутренних механизмов сессии.
