# Встраивание EpochSim

Этот документ про интеграцию в приложение, а не про низкоуровневую настройку builder.

## RunTicks vs Session

`RunTicks` для ограниченного batch-прогона:

```csharp
engine.RunTicks(state, seed: 1, endTickInclusive: 10_000);
```

`CreateSession` для долгоживущих процессов (серверы/игры/интерактив):

```csharp
using var session = engine.CreateSession(state, seed: 1, startTick: 0);
session.TickOnce();
session.RunUntil(1_000);
```

`Session` сохраняет непрерывность RNG и scheduler между многими `TickOnce`.

## Где живет состояние

- Runtime-state живет в памяти (`TState`).
- Persisted state опционален и идет через артефакты:
  - `statefp.jsonl` для легкого сравнения прогонов;
  - `snapshots/` для точек восстановления.
- `manifest.json` и `meta.txt` фиксируют конфигурацию запуска.

Минимальная интеграция (без codec/event):

```csharp
var state = new WorldState();
var engine = Epoch.CreateEngine<WorldState>();
engine.AddSystem("World", tick: ctx => ctx.State.Population++);

using var run = Epoch.QuickRun(state, rootDir: "artifacts");
engine.Attach(run);
engine.RunTicks(state, seed: 1, endTickInclusive: 100);
```

## Внешние входы и поток команд

Паттерн:
1. Продюсеры (HTTP/UI/queue) кладут команды в inbox.
2. Поток симуляции дренирует inbox в начале тика.
3. Команды обрабатываются в детерминированном pump-цикле.

### Рецепт: single-thread loop

```csharp
using EpochSim;
using EpochSim.Kernel.Messaging;

var state = new WorldState();
var engine = Epoch.CreateEngine<WorldState>();
var inbox = new CommandInbox();

engine.AttachInbox(inbox);
engine.OnCommand<AddPopulation>((s, cmd, _) => s.Population += cmd.Delta);

using var run = Epoch.QuickRun(state, "artifacts");
engine.Attach(run);

using var session = engine.CreateSession(state, seed: 1, startTick: 0);
inbox.Enqueue(new AddPopulation(5));
session.TickOnce();
```

### Рецепт: server pattern с расписанием по тикам

```csharp
using EpochSim;
using EpochSim.Kernel.Messaging;

var state = new WorldState();
var engine = Epoch.CreateEngine<WorldState>();
var scheduledInbox = new ScheduledCommandInbox();

engine.AttachScheduledInbox(scheduledInbox);
engine.OnCommand<AddPopulation>((s, cmd, _) => s.Population += cmd.Delta);

using var session = engine.CreateSession(state, seed: 1, startTick: 0);

scheduledInbox.Enqueue(10, new AddPopulation(2));
scheduledInbox.Enqueue(10, new AddPopulation(3));
scheduledInbox.Enqueue(11, new AddPopulation(1));

session.RunUntil(20);
```

`ScheduledCommandInbox` гарантирует порядок:
- `tick` по возрастанию;
- затем порядок добавления.

## Какие артефакты включать в проде

Рекомендация:
- `QuickRun`: локальный smoke/integration.
- `RecommendedRun`: базовый набор для отладки сервиса.

`RecommendedRun` включает:
- fingerprints каждый тик;
- snapshots по интервалу;
- trace + profiling;
- failure artifacts при падениях.

Event log включайте только когда нужен replay/bisect workflow.

## Отладка детерминизма через CLI

Запуск:

```bash
dotnet run --project src/EpochSim.Cli -- run artifacts
```

Проверка fingerprint-эквивалентности:

```bash
dotnet run --project src/EpochSim.Cli -- verify-run artifacts <runId>
```

Поиск первого тика расхождения:

```bash
dotnet run --project src/EpochSim.Cli -- bisect artifacts <runId>
```

Replay от лучшего snapshot + events:

```bash
dotnet run --project src/EpochSim.Cli -- fast-replay artifacts <runId>
```

## Продвинутая настройка

Используйте `Epoch.RecommendedRun(state, codec, serializer, ...)` или `EpochSimRun.For(state)`, когда нужен явный контроль codec/runId/настроек артефактов.

Связанные документы:
- [Artifacts.ru](Artifacts.ru.md)
- [Sessions.ru](Sessions.ru.md)
- [Determinism.ru](Determinism.ru.md)
