# EpochSim

Детерминированный движок тиковых симуляций для .NET 10.

`EpochSim` — основной фасадный пакет: минимум ручной обвязки и быстрый старт для встраивания.

English version: [README.md](README.md)

## Установка

```bash
dotnet add package EpochSim
```

Если нужен полный граф модулей одной зависимостью:

```bash
dotnet add package EpochSim.All
```

## Быстрый старт за 60 секунд

```csharp
using EpochSim;
using EpochSim.Kernel.Messaging;

var state = new WorldState();
var engine = Epoch.CreateEngine<WorldState>();

engine.AddSystem(
    "World",
    tick: ctx => ctx.Commands.Enqueue(new Grow(1)),
    handle: (ctx, ev) =>
    {
        if (ev is Grew e)
            ctx.State.Population += e.Delta;
    });

engine.OnCommand<Grow>((_, cmd, events) => events.Emit(new Grew(cmd.Delta)));

var codec = Epoch.JsonCodecFromAssembly<Program>(t => t == typeof(Grew));
var serializer = Epoch.JsonStateSerializer<WorldState>();

using var run = Epoch.RecommendedRun(state, codec, serializer, rootDir: "artifacts");
engine.Attach(run);
engine.RunTicks(state, seed: 12345, endTickInclusive: 100);

Console.WriteLine($"RunId={run.RunId}");
Console.WriteLine($"RunDir={run.Paths.RunDir}");

public sealed class WorldState
{
    public int Population { get; set; }
}

[MessageKind("Grow")]
public sealed record Grow(int Delta) : ICommand;
public sealed record Grew(int Delta) : IEvent;
```

Результат: детерминированный прогон в `artifacts/<runId>/` со стандартными артефактами.

## Что упрощено

- Меньше boilerplate в домене:
  - для `IEvent` и `ICommand` не нужно вручную писать `Kind`;
  - у `ISystem<TState>` есть дефолтные `Name` и no-op `Handle`.
- Lambda-регистрация:
  - `engine.AddSystem(name, tick, handle?)`
  - `engine.OnCommand<TCommand>((state, cmd, events) => ...)`
- Простые перегрузки запуска:
  - `RunTicks(state, seed, endTickInclusive)`
  - `RunTicks(state, seed, startTick, endTickInclusive)`
- Пресеты артефактов:
  - `EpochSimRun.Quick(...)`
  - `EpochSimRun.Recommended(...)`

## Пресеты запуска

- Quick:
  - `EpochSimRun.Quick(state, codec?, serializer?, rootDir?)`
  - Для быстрого smoke-прогона.
- Recommended:
  - `EpochSimRun.Recommended(state, codec, serializer, rootDir?, invariants?)`
  - Включает compression, event log, snapshots, fingerprints, trace, profiling, mutation guard, failure artifacts.

## API долгоживущей сессии

```csharp
using EpochSim.Kernel.Time;

using var session = engine.CreateSession(state, seed: 12345, start: SimTime.Zero);

while (session.CurrentTime.Tick <= 10)
    session.TickOnce();

session.RunUntil(new SimTime(500));
```

Поверхность API:
- `SimTime CurrentTime { get; }`
- `bool TickOnce(CancellationToken ct = default)`
- `void RunUntil(SimTime endInclusive, CancellationToken ct = default)`

## Полный builder (явная настройка)

```csharp
using EpochSim;
using EpochSim.Execution.RunArtifacts;
using EpochSim.Kernel.Validation;

var serializer = Epoch.JsonStateSerializer<WorldState>();
var codec = Epoch.JsonCodecFromAssembly<Program>(t => t == typeof(Grew));
IInvariant<WorldState>[] invariants = [];

using var run = EpochSimRun.For(state)
    .WithRootDirectory("artifacts")
    .WithRunId(RunId.New())
    .WithRecommendedDefaults(codec, serializer, invariants)
    .Build();

engine.Attach(run);
engine.RunTicks(state, seed: 12345, endTickInclusive: 100);
```

## CLI быстрый старт

```bash
dotnet run --project src/EpochSim.Cli -- init .
dotnet run --project src/EpochSim.Cli -- run artifacts
dotnet run --project src/EpochSim.Cli -- list-runs artifacts
```

## Чек-лист детерминизма

Избегайте в системах и обработчиках:
- `DateTime.Now` / `DateTime.UtcNow`
- `Guid.NewGuid()`
- произвольного `Random` (используйте `ctx.Rng`)
- зависимости от порядка неупорядоченных коллекций
- скрытых гонок многопоточности

## Сборка и тесты

```bash
dotnet build EpochSim.slnx -m:1
dotnet test EpochSim.slnx -m:1
```

## Документация

Русская:
- [Embedding.ru](docs/Embedding.ru.md)
- [Artifacts.ru](docs/Artifacts.ru.md)
- [Sessions.ru](docs/Sessions.ru.md)
- [Determinism.ru](docs/Determinism.ru.md)

English:
- [README.md](README.md)
- [Embedding](docs/Embedding.md)
- [Artifacts](docs/Artifacts.md)
- [Sessions](docs/Sessions.md)
- [Determinism](docs/Determinism.md)
