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
run.RunTicks(engine, seed: 1, endTickInclusive: 100);

Console.WriteLine(state.Population);

public sealed class WorldState
{
    public int Population { get; set; }
}
```

> **Важно**: всегда вызывайте `run.RunTicks(engine, ...)`, а не `engine.RunTicks(state, ...)` —
> иначе можно передать другой экземпляр state, чем тот, с которым был создан run scope.

Это самый быстрый путь: полезные артефакты и детерминированный запуск почти без обвязки.

## Таблица стоимости пресетов

| Пресет | Каденция fingerprint | Mutation guard | Когда использовать |
|---|---|---|---|
| `QuickRun` | каждый тик | ✗ | Первый запуск, CI smoke |
| `RecommendedRun` | каждые 50 тиков | ✗ | Production-отладка |
| `DebugRun` | каждый тик | ✓ | Поиск инвариантов, строгий режим |

**Mutation guard** сериализует state до и после каждого тика системы и бросает
`InvalidOperationException` при изменении fingerprint. Полезен для нахождения систем,
мутирующих state вне event handlers, но имеет значительный overhead по сериализации —
используйте только в `DebugRun`.

```csharp
// QuickRun — без конфигурации, кодек не нужен
using var run = Epoch.QuickRun(state, rootDir: "artifacts");
run.RunTicks(engine, seed: 1, endTickInclusive: 100);

// RecommendedRun — добавляет profiling + failure artifacts
using var run = Epoch.RecommendedRun(state, rootDir: "artifacts");
run.RunTicks(engine, seed: 1, endTickInclusive: 1000);

// DebugRun — fingerprint каждый тик + mutation guard
var serializer = Epoch.JsonStateSerializer<WorldState>();
using var run = Epoch.DebugRun(state, serializer, rootDir: "artifacts");
run.RunTicks(engine, seed: 1, endTickInclusive: 200);
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

## Чтение артефактов из кода

После завершения прогона артефакты можно читать непосредственно из кода, без CLI:

```csharp
using EpochSim;
using EpochSim.Execution.RunArtifacts;
using EpochSim.Execution.StateFingerprint;
using EpochSim.Serialization.EventLog;
using EpochSim.Serialization.State;

var state = new WorldState();
var serializer = Epoch.JsonStateSerializer<WorldState>();
var codec = Epoch.JsonCodecFromAssembly<Program>(t => t == typeof(Changed));
var engine = Epoch.CreateEngine<WorldState>();
engine.AddSystem("World", tick: ctx => ctx.State.Population++);

string runDir;
using (var run = Epoch.RecommendedRun(state, codec, serializer, rootDir: "artifacts"))
{
    run.RunTicks(engine, seed: 1, endTickInclusive: 100);
    runDir = run.Paths.RunDir;
}

// Читаем манифест, записанный при Dispose()
var manifest = RunManifestReader.TryRead(Path.Combine(runDir, "manifest.json"));
Console.WriteLine($"EndTick={manifest?.EndTick}");

// Читаем per-tick fingerprints
var fingerprints = JsonlStateFingerprintWriter.ReadAll(Path.Combine(runDir, "statefp.jsonl"));
Console.WriteLine($"Fingerprints recorded: {fingerprints.Count}");

// Читаем лог событий
var entries = EventLogReader.ReadAll(Path.Combine(runDir, "events.jsonl"));
Console.WriteLine($"Events logged: {entries.Count}");
```

## RunTicks и Session

`RunTicks` для batch-прогонов:

```csharp
run.RunTicks(engine, seed: 1, endTickInclusive: 1000);
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

- Простой пример уровня README: [QuickstartSample](src/EpochSim.Samples/Quickstart/QuickstartSample.cs)
- Продвинутый/legacy пример домена: [Population](src/EpochSim.Samples/Population)

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
