# EpochSim

Детерминированный движок тиковых симуляций для .NET 10.

Рекомендуемый пакет для встраивания:

```bash
dotnet add package EpochSim
```

`EpochSim.All` полезен, когда нужен полный граф модулей одной зависимостью:

```bash
dotnet add package EpochSim.All
```

English version: [README.md](README.md)

## Быстрый старт только со State (без Event/Codec)

```csharp
using EpochSim;

var state = new WorldState();
var engine = Epoch.CreateEngine<WorldState>();
engine.AddSystem("World", tick: ctx => ctx.State.Population++);

using var run = Epoch.QuickRun(state, rootDir: "artifacts");
engine.Attach(run);
engine.RunTicks(state, seed: 1, endTickInclusive: 100);

Console.WriteLine(state.Population);

public sealed class WorldState
{
    public int Population { get; set; }
}
```

Это самый быстрый путь: полезные артефакты и детерминированный запуск почти без обвязки.

## Рекомендуемые пресеты артефактов

`QuickRun`:
- лучший для первого запуска и базовой отладки;
- включает metadata + snapshots + state fingerprints + trace;
- не требует event codec.

```csharp
using var run = Epoch.QuickRun(state, rootDir: "artifacts");
```

`RecommendedRun`:
- базовый вариант для реальной отладки в приложениях и сервисах;
- включает fingerprints + snapshots + trace + profiling + failure artifacts;
- не требует codec в базовом сценарии;
- можно добавить инварианты.

```csharp
using var run = Epoch.RecommendedRun(state, rootDir: "artifacts", invariants: null);
```

Продвинутые варианты:
- `Epoch.RecommendedRun(state, codec, serializer, rootDir, invariants)`
- `EpochSimRun.For(state)...Build()`

## Event log / Replay (когда действительно нужно)

Event log нужен для replay/bisect (`verify-run`, `bisect`, `fast-replay`):

```csharp
using EpochSim;
using EpochSim.Kernel.Messaging;

var codec = Epoch.JsonCodecFromAssembly<Program>(t => t == typeof(Changed));
var serializer = Epoch.JsonStateSerializer<WorldState>();

using var run = Epoch.RecommendedRun(state, codec, serializer, rootDir: "artifacts");
```

```csharp
[MessageKind("Changed")]
public sealed record Changed(int Delta) : IEvent;
```

## RunTicks и Session

`RunTicks` для batch-прогонов:

```csharp
engine.RunTicks(state, seed: 1, endTickInclusive: 1000);
```

`SimulationSession<TState>` для долгоживущих циклов:

```csharp
var inbox = new CommandInbox();
engine.AttachInbox(inbox);

using var session = engine.CreateSession(state, seed: 1, startTick: 0);
inbox.Enqueue(new AddPopulation(5));
session.TickOnce();
session.RunUntil(100);
```

```csharp
using EpochSim.Kernel.Messaging;
[MessageKind("AddPopulation")]
public sealed record AddPopulation(int Delta) : ICommand;
```

## Паттерн внешних входов (HTTP/UI/Queue -> sim)

Используйте inbox’ы для детерминированного ввода команд в тике симуляции:
- `CommandInbox`: команды на ближайший тик.
- `ScheduledCommandInbox`: команды на заданный тик со стабильным порядком:
  - сначала `tick` по возрастанию;
  - затем порядок добавления.

```csharp
var scheduled = new ScheduledCommandInbox();
engine.AttachScheduledInbox(scheduled);

scheduled.Enqueue(10, new AddPopulation(3));
scheduled.Enqueue(10, new AddPopulation(7));
scheduled.Enqueue(12, new AddPopulation(1));
```

Полные рецепты: [Embedding](docs/Embedding.ru.md)

## CLI старт

```bash
dotnet run --project src/EpochSim.Cli -- init .
dotnet run --project src/EpochSim.Cli -- run artifacts
dotnet run --project src/EpochSim.Cli -- list-runs artifacts
```

Отладка детерминизма:

```bash
dotnet run --project src/EpochSim.Cli -- verify-run artifacts <runId>
dotnet run --project src/EpochSim.Cli -- bisect artifacts <runId>
```

## Примеры

- Простой пример уровня README: `src/EpochSim.Samples/Quickstart/QuickstartSample.cs`
- Продвинутый/legacy пример домена: `src/EpochSim.Samples/Population/`

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
