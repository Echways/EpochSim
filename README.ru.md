<div align="center">

# EpochSim

**Детерминированный движок тиковых симуляций для .NET 10.**
Запускайте симуляцию по seed, записывайте fingerprint'ы и события, затем проверяйте, воспроизводите и бисектите любой прогон.

[English](README.md) · **Русский**

[![NuGet](https://img.shields.io/nuget/v/EpochSim)](https://www.nuget.org/packages/EpochSim)
[![CI](https://github.com/Echways/EpochSim/actions/workflows/ci.yml/badge.svg)](https://github.com/Echways/EpochSim/actions/workflows/ci.yml)
[![License](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

</div>

## Зачем нужен EpochSim

Симуляцию, которую нельзя воспроизвести, нельзя отладить. EpochSim фиксирует входы каждого тика: один и тот же seed и те же команды всегда дают то же состояние, а расхождение локализуется до одного тика.

- **Поиск seed, на котором ломается логика.** `Epoch.Fuzz` прогоняет симуляцию на тысячах seed, возвращает сбойный seed и тик и показывает, какие поля состояния изменились.
- **Детерминизм по построению.** Прогон определяется состоянием, seed и упорядоченным входом. Системы выполняются в фиксированном порядке в одном потоке симуляции.
- **Пресеты запуска.** `QuickRun`, `RecommendedRun` и `DebugRun` закрывают smoke-тесты, диагностику в production и строгий поиск нарушений инвариантов одним вызовом.
- **Артефакты каждого прогона.** Манифест, fingerprint'ы состояния по тикам и необязательный лог событий пишутся на диск и читаются из кода.
- **Replay и bisect.** Проверка прогона по записанным fingerprint'ам и поиск первого тика, на котором нарушается инвариант.
- **Детерминированный ввод.** `CommandInbox` и `ScheduledCommandInbox` передают команды из HTTP, UI или очередей в стабильном порядке.
- **Долгоживущие сессии.** `SimulationSession<TState>` для игр, серверов и пошагового режима.
- **CLI для всего процесса.** Запуск, список, инспекция, валидация, replay, bisect и воспроизведение прогонов из терминала.

## Установка

```bash
dotnet add package EpochSim
```

Требуется .NET 10. `EpochSim.All` приносит весь граф модулей (`EpochSim`, `Execution`, `Serialization`, `Observability`, `Kernel`) одной зависимостью:

```bash
dotnet add package EpochSim.All
```

Утилита командной строки ставится отдельно:

```bash
dotnet tool install --global EpochSim.Cli
```

## Быстрый старт

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

Всегда вызывайте `run.RunTicks(engine, ...)`, а не `engine.RunTicks(state, ...)`: так передаётся тот же state, с которым создан run scope.

Подробнее в [Embedding](docs/ru/embedding.md).

## Поиск багов по seed

`Epoch.Fuzz` прогоняет ваш код на диапазоне seed и проверяет инварианты, исключения и, по желанию, детерминизм. Переписывать существующий код не нужно: вызовите его из `Step`, опишите в `Invariant`, что должно выполняться всегда, и задайте seed. `ctx.Rng.AsRandom()` отдаёт коду, принимающему `System.Random`, детерминированный поток.

<!-- snippet: throws FuzzFailedException -->
```csharp
using EpochSim;

var report = Epoch.Fuzz<Inventory>(f => f
    .State(() => new Inventory())
    .Step((inventory, ctx) => inventory.Tick(ctx.Rng.AsRandom()))
    .Invariant("StockNonNegative", inventory => inventory.Stock >= 0)
    .Seeds(1, 1000)
    .Ticks(500));

report.ThrowIfFailed();

public sealed class Inventory
{
    public int Stock { get; private set; } = 5;

    public void Tick(Random random)
    {
        if (random.Next(0, 3) == 0)
            Stock += 4;

        var demand = random.Next(0, 3);
        if (Stock >= demand)
            Stock -= demand;

        if (random.Next(0, 400) == 0)
            Stock -= 5;
    }
}
```

`Inventory` это обычный класс без ссылок на EpochSim. Прогон находит seed, на которых остаток уходит в минус, и называет тик и изменившиеся поля состояния:

```text
Fuzz failed: Failures=21, SeedsRun=1000, ElapsedMs=150

[InvariantViolation] seed=18 tick=19
  StockNonNegative: predicate returned false
  StateDiff:
    $.Stock: 1 -> -2
  Repro: .Seeds(18, 18).Ticks(19)

...
```

Добавьте `.Seeds(18, 18).Ticks(19)` из строки `Repro` в свой вызов, чтобы прогнать только этот сбой. Если симуляцию ведут команды, сбои при `Commands(...)` сокращаются до минимального сценария, `FailureStore()` хранит сбойные seed в файле, который вы коммитите, а `epochsim failures export-test` превращает их в регрессионные тесты. `.CheckDeterminism()` прогоняет каждый seed дважды и ловит `static`-состояние, системное время и другие источники недетерминизма. Тот же поиск из терминала:

```bash
epochsim fuzz --adapter fuzzbug --seeds 1..1000 --ticks 500
```

Подробнее в [Fuzzing](docs/ru/fuzzing.md).

## Тестирование

`EpochSim.Testing` даёт те же проверки в xUnit, NUnit и MSTest, не завися ни от одного из них:

```bash
dotnet add package EpochSim.Testing
```

```csharp
using EpochSim.Testing;

SimAssert.IsDeterministic<WorldState>(f => f
    .State(() => new WorldState())
    .Step((world, _) => world.Population++)
    .Seeds(1, 50)
    .Ticks(200));

public sealed class WorldState
{
    public int Population { get; set; }
}
```

Сообщение о сбое называет seed, тик и изменившиеся поля состояния. Подробнее в [Testing](docs/ru/testing.md).

## Пресеты запуска

| Пресет           | Каденция fingerprint | Mutation guard | Когда использовать                |
| ---------------- | -------------------- | -------------- | --------------------------------- |
| `QuickRun`       | каждый тик           | Нет            | Первый запуск, CI smoke           |
| `RecommendedRun` | каждые 50 тиков      | Нет            | Диагностика в production          |
| `DebugRun`       | каждый тик           | Да             | Поиск инвариантов, строгий режим  |

```csharp
using EpochSim;

var state = new WorldState();
var engine = Epoch.CreateEngine<WorldState>();
engine.AddSystem("World", tick: ctx => ctx.State.Population++);

using var quick = Epoch.QuickRun(state, rootDir: "artifacts");
quick.RunTicks(engine, seed: 1, endTickInclusive: 100);

using var recommended = Epoch.RecommendedRun(state, rootDir: "artifacts");
recommended.RunTicks(engine, seed: 1, endTickInclusive: 1000);

public sealed class WorldState
{
    public int Population { get; set; }
}
```

Mutation guard сериализует state до и после каждой системы и бросает `InvalidOperationException`, если fingerprint изменился. Он находит системы, меняющие state вне обработчиков событий, но заметно замедляет прогон: используйте его только в `DebugRun`. Система выше меняет `Population` напрямую, поэтому `DebugRun` отклоняет её, называя тик и систему:

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

Для полного контроля: `Epoch.RecommendedRun(state, codec, serializer, rootDir, invariants)` или `EpochSimRun.For(state)...Build()`.

## Лог событий и артефакты

Лог событий необязателен и нужен для `verify-run`, `bisect` и `fast-replay`. Артефакты лежат обычными файлами в каталоге прогона и читаются из кода:

```csharp
using EpochSim;
using EpochSim.Execution.RunArtifacts;
using EpochSim.Execution.StateFingerprint;
using EpochSim.Kernel.Messaging;
using EpochSim.Serialization.EventLog;

var state = new WorldState();
var engine = Epoch.CreateEngine<WorldState>();
var inbox = new ScheduledCommandInbox();
engine.AttachScheduledInbox(inbox);
engine.OnCommand<AddPopulation>((world, command, events) =>
{
    world.Population += command.Delta;
    events.Emit(new Changed(command.Delta));
});
inbox.Enqueue(5, new AddPopulation(3));

var codec = Epoch.JsonCodecFromAssembly<Program>(type => type == typeof(Changed));
var serializer = Epoch.JsonStateSerializer<WorldState>();

string runDir;
using (var run = Epoch.RecommendedRun(state, codec, serializer, rootDir: "artifacts"))
{
    run.RunTicks(engine, seed: 1, endTickInclusive: 100);
    runDir = run.Paths.RunDir;
}

var manifest = RunManifestReader.TryRead(Path.Combine(runDir, "manifest.json"));
var fingerprints = JsonlStateFingerprintWriter.ReadAll(Path.Combine(runDir, "statefp.jsonl"));
var entries = EventLogReader.ReadAll(Path.Combine(runDir, "events.jsonl"));

Console.WriteLine($"EndTick={manifest?.EndTick}, fingerprints={fingerprints.Count}, events={entries.Count}");

[MessageKind("AddPopulation")]
public sealed record AddPopulation(int Delta) : ICommand;

[MessageKind("Changed")]
public sealed record Changed(int Delta) : IEvent;

public sealed class WorldState
{
    public int Population { get; set; }
}
```

Структура и форматы описаны в [Artifacts](docs/ru/artifacts.md).

## Сессии и внешний ввод

`RunTicks` подходит для batch-прогонов. `SimulationSession<TState>` подходит для долгоживущих циклов:

```csharp
using EpochSim;
using EpochSim.Kernel.Messaging;

var state = new WorldState();
var engine = Epoch.CreateEngine<WorldState>();
engine.OnCommand<AddPopulation>((world, command, _) => world.Population += command.Delta);

var inbox = new CommandInbox();
engine.AttachInbox(inbox);

using var session = engine.CreateSession(state, seed: 1, startTick: 0);
inbox.Enqueue(new AddPopulation(5));
session.TickOnce();
session.RunUntil(100);

[MessageKind("AddPopulation")]
public sealed record AddPopulation(int Delta) : ICommand;

public sealed class WorldState
{
    public int Population { get; set; }
}
```

Inbox'ы детерминированно передают внешние команды в поток симуляции:

- `CommandInbox`: команды на ближайший тик.
- `ScheduledCommandInbox`: команды на заданный тик, порядок: сначала тик по возрастанию, затем порядок добавления.

```csharp
using EpochSim;
using EpochSim.Kernel.Messaging;

var state = new WorldState();
var engine = Epoch.CreateEngine<WorldState>();
engine.OnCommand<AddPopulation>((world, command, _) => world.Population += command.Delta);

var scheduled = new ScheduledCommandInbox();
engine.AttachScheduledInbox(scheduled);

scheduled.Enqueue(10, new AddPopulation(3));
scheduled.Enqueue(10, new AddPopulation(7));
scheduled.Enqueue(12, new AddPopulation(1));

using var session = engine.CreateSession(state, seed: 1, startTick: 0);
session.RunUntil(100);

[MessageKind("AddPopulation")]
public sealed record AddPopulation(int Delta) : ICommand;

public sealed class WorldState
{
    public int Population { get; set; }
}
```

Подробнее в [Sessions](docs/ru/sessions.md).

## Командная строка

```bash
epochsim init .
epochsim run artifacts
epochsim list-runs artifacts
epochsim inspect-run artifacts <runId>
```

Отладка детерминизма:

```bash
epochsim verify-run artifacts <runId>
epochsim bisect artifacts <runId>
epochsim fuzz --adapter fuzzbug --seeds 1..1000
epochsim failures list
```

| Команда                                      | Назначение                                      |
| -------------------------------------------- | ----------------------------------------------- |
| `init`, `run`                                | Создать стартовое приложение, запустить его     |
| `list-runs`, `inspect-run`, `pretty-inspect` | Просмотр и инспекция записанных прогонов        |
| `validate-run`, `verify-run`                 | Проверка артефактов и fingerprint'ов            |
| `fast-replay`, `bisect`, `repro`             | Replay прогона, поиск первого нарушения         |
| `fuzz`, `failures`                           | Поиск сбойного seed, сохранённые сбои           |
| `event-stats`, `timeline`, `list-adapters`   | Анализ событий и список доступных адаптеров     |

## Состав

| Область       | Возможности                                                                    |
| ------------- | ------------------------------------------------------------------------------ |
| Движок        | Упорядоченные системы, прогоны по seed, выполнение по тикам и сессиям.         |
| Пресеты       | `QuickRun`, `RecommendedRun`, `DebugRun`, builder для своих конфигураций.      |
| Ввод          | `CommandInbox`, `ScheduledCommandInbox` со стабильным порядком.                |
| Артефакты     | Манифест, fingerprint'ы состояния, лог событий, артефакты сбоев, профилирование. |
| Сериализация  | JSON-сериализатор состояния и кодек сообщений, находимых в сборке.             |
| Диагностика   | Mutation guard, инварианты, проверка replay, bisect, полевой дифф состояния.   |
| Анализатор    | Roslyn-правила ESIM001-ESIM006 ловят системное время, `Random`, потоки и другое в коде симуляции. |
| Тестирование  | `EpochSim.Testing`: `SimAssert` для любого тестового фреймворка, золотые fingerprint'ы. |
| Fuzzing       | `Epoch.Fuzz`: диапазоны seed, инварианты, проверка детерминизма, шринкинг, хранилище сбоев, регрессионные тесты. |
| CLI           | .NET-утилита `epochsim` для всего жизненного цикла прогона.                    |

## Документация

|                                    |                                                        |
| ---------------------------------- | ------------------------------------------------------ |
| [Embedding](docs/ru/embedding.md)  | Рецепты интеграции: хосты, серверы, внешний ввод       |
| [Artifacts](docs/ru/artifacts.md)  | Структура каталога прогона и форматы файлов            |
| [Sessions](docs/ru/sessions.md)    | Долгоживущие циклы и пошаговый режим                   |
| [Determinism](docs/ru/determinism.md) | Правила, сохраняющие воспроизводимость прогонов     |
| [Fuzzing](docs/ru/fuzzing.md)      | Поиск сбойных seed и их воспроизведение                |
| [Testing](docs/ru/testing.md)      | `SimAssert` для xUnit, NUnit и MSTest                  |

## Примеры

[QuickstartSample](src/EpochSim.Samples/Quickstart/QuickstartSample.cs) — минимальная отправная точка. [Population](src/EpochSim.Samples/Population/) — событийный домен с инвариантами и replay. [Onboarding](src/EpochSim.Samples/Onboarding/) — класс `Inventory` из примера поиска багов. [Fuzzing](src/EpochSim.Samples/Fuzzing/) — редкие баги, зависящие от seed и от команд, для поиска через `Epoch.Fuzz`.

## Сборка и тесты

```bash
dotnet build EpochSim.slnx -m:1
dotnet test EpochSim.slnx -m:1
```

## Лицензия

[MIT](LICENSE)
