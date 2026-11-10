# Встраивание

Интеграция EpochSim в приложение: модель выполнения, внешний ввод и диагностика.

## Batch-прогоны и сессии

`RunTicks` выполняет ограниченный batch-прогон:

```csharp
using EpochSim;

var state = new WorldState();
var engine = Epoch.CreateEngine<WorldState>();
engine.AddSystem("World", tick: ctx => ctx.State.Population++);

engine.RunTicks(state, seed: 1, endTickInclusive: 10_000);

public sealed class WorldState
{
    public int Population { get; set; }
}
```

`CreateSession` подходит для долгоживущих приложений: серверов, игр и интерактивных инструментов:

```csharp
using EpochSim;

var state = new WorldState();
var engine = Epoch.CreateEngine<WorldState>();
engine.AddSystem("World", tick: ctx => ctx.State.Population++);

using var session = engine.CreateSession(state, seed: 1, startTick: 0);
session.TickOnce();
session.RunUntil(1_000);

public sealed class WorldState
{
    public int Population { get; set; }
}
```

Сессия сохраняет непрерывность генератора случайных чисел и планировщика между вызовами `TickOnce`. См. [Sessions](sessions.md).

## Состояние и хранение

- Рабочее состояние — это `TState` в памяти, детерминированно изменяемый системами и обработчиками.
- Постоянное хранение необязательно и выполняется через артефакты прогона: `statefp.jsonl` для fingerprint'ов и `snapshots/` для снимков состояния.
- `manifest.json` и `meta.txt` описывают настройки и жизненный цикл прогона.

Минимальной интеграции не нужны ни кодек, ни события:

```csharp
using EpochSim;

var state = new WorldState();
var engine = Epoch.CreateEngine<WorldState>();
engine.AddSystem("World", tick: ctx => ctx.State.Population++);

using var run = Epoch.QuickRun(state, rootDir: "artifacts");
run.RunTicks(engine, seed: 1, endTickInclusive: 100);

public sealed class WorldState
{
    public int Population { get; set; }
}
```

Запускайте через `run.RunTicks(engine, ...)`, чтобы использовался тот state, с которым создан run scope. Другой экземпляр, переданный тому же движку, приведёт к ошибке несоответствия state.

## Внешний ввод

1. Источники — HTTP-эндпоинты, очереди, UI — помещают команды в inbox.
2. Поток симуляции забирает inbox на границах тиков.
3. Движок обрабатывает команды внутри своего детерминированного цикла.

### Цикл в одном потоке

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

[MessageKind("AddPopulation")]
public sealed record AddPopulation(int Delta) : ICommand;

public sealed class WorldState
{
    public int Population { get; set; }
}
```

### Сервер с отложенным вводом

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

[MessageKind("AddPopulation")]
public sealed record AddPopulation(int Delta) : ICommand;

public sealed class WorldState
{
    public int Population { get; set; }
}
```

Порядок `ScheduledCommandInbox` детерминирован: сначала тик по возрастанию, затем порядок добавления внутри тика.

## Выбор пресета

| Пресет           | Назначение                                        |
| ---------------- | ------------------------------------------------- |
| `QuickRun`       | Локальная интеграция и smoke-проверки             |
| `RecommendedRun` | База для сервисов и диагностики в production      |
| `DebugRun`       | Fingerprint'ы каждый тик и mutation guard         |

`RecommendedRun` включает:

- сжатие артефактов
- fingerprint'ы состояния каждые 50 тиков
- снимки каждые 50 тиков
- потоки trace и profiling
- артефакты сбоев при падении
- лог событий, если передан кодек
- проверку инвариантов каждый тик, если переданы инварианты

Включайте лог событий только когда нужны replay или bisect. Для явного управления кодеком, идентификатором прогона и отдельными артефактами используйте `Epoch.RecommendedRun(state, codec, serializer, ...)` или `EpochSimRun.For(state)`.

### Mutation guard

`DebugRun` сериализует state до и после каждой системы и бросает `InvalidOperationException`, если fingerprint изменился. Он находит системы, меняющие state вне обработчиков событий, но заметно замедляет прогон: используйте его только в `DebugRun`. Система ниже меняет `Population` напрямую, поэтому `DebugRun` отклоняет её, называя тик и систему:

<!-- snippet: throws InvalidOperationException -->
```csharp
using EpochSim;

var state = new WorldState();
var engine = Epoch.CreateEngine<WorldState>();
engine.AddSystem("World", tick: ctx => ctx.State.Population++);

using var debug = Epoch.DebugRun(state, rootDir: "artifacts");
debug.RunTicks(engine, seed: 1, endTickInclusive: 200);

public sealed class WorldState
{
    public int Population { get; set; }
}
```

## Отладка детерминизма

Запустить симуляцию:

```bash
epochsim run artifacts
```

Сверить fingerprint'ы с записанным прогоном:

```bash
epochsim verify-run artifacts <runId>
```

Найти первый тик расхождения прогонов:

```bash
epochsim bisect artifacts <runId>
```

Воспроизвести от ближайшего снимка и по логу событий:

```bash
epochsim fast-replay artifacts <runId>
```

## См. также

- [Artifacts](artifacts.md)
- [Sessions](sessions.md)
- [Determinism](determinism.md)
