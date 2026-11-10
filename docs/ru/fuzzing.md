# Fuzzing

`Epoch.Fuzz` прогоняет ваш код на диапазоне seed и показывает, какой seed его ломает, на каком тике и какие поля состояния изменились на этом тике. Находит три вида сбоев:

- `InvariantViolation`: инвариант вернул `false`.
- `Exception`: исключение из системы, шага или обработчика команды.
- `NonDeterminism`: два прогона одного seed дали разное состояние (нужен `CheckDeterminism`).

Любой сбой воспроизводится тем же seed. Если симуляцию ведут команды, отчёт ещё и сокращает их до минимального сценария ([Шринкинг](#шринкинг)), а сбойные seed можно хранить в файле, чтобы найденный один раз баг проверялся всегда ([Хранилище сбоев](#хранилище-сбоев)).

## Минимальный пример

`Inventory` это обычный класс, который ничего не знает про EpochSim. `Step` вызывает его на каждом тике, `Invariant` говорит, что должно выполняться всегда, `Seeds` и `Ticks` задают объём перебора:

```csharp
using EpochSim;

var report = Epoch.Fuzz<Inventory>(f => f
    .State(() => new Inventory())
    .Step((inventory, ctx) => inventory.Tick(ctx.Rng.AsRandom()))
    .Invariant("StockNonNegative", inventory => inventory.Stock >= 0)
    .Seeds(1, 1000)
    .Ticks(500));

Console.WriteLine(report.ToText());

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

Вывод (первый из 21 сбоя; `ElapsedMs` зависит от машины):

```text
Fuzz failed: Failures=21, SeedsRun=1000, ElapsedMs=150

[InvariantViolation] seed=18 tick=19
  StockNonNegative: predicate returned false
  StateDiff:
    $.Stock: 1 -> -2
  Repro: .Seeds(18, 18).Ticks(19)
```

В тесте вызывайте `report.ThrowIfFailed()` или используйте [EpochSim.Testing](testing.md). Этот же класс лежит в [src/EpochSim.Samples/Onboarding](../../src/EpochSim.Samples/Onboarding/), и набор тестов прогоняет на нём вызов выше.

## Подключение существующего кода

Переносить код в системы, классы инвариантов и сериализуемое состояние не нужно.

| Метод                                   | Что делает                                                                                             |
| --------------------------------------- | ------------------------------------------------------------------------------------------------------ |
| `Step(Action<TState, StepContext>)`     | Вызывается на каждом тике после систем, добавленных через `Engine`. Несколько шагов идут в порядке регистрации. |
| `Invariant(string, Func<TState, bool>)` | Именованный инвариант, проверяется после каждого тика. Имя попадает в отчёт и в `Signature`.            |
| `ctx.Rng.AsRandom()`                    | `System.Random` поверх детерминированного потока, для кода, который уже принимает `Random`.             |
| `ctx.TimeProvider`                      | `TimeProvider` (`FakeTimeProvider`), стартует с `StepContext.ClockStart` и продвигается на `StepContext.TickDuration` (одну секунду) за тик. |
| `ctx.Tick`                              | Текущий тик.                                                                                           |
| `Project(Func<TState, object>)`         | Часть состояния для fingerprint'ов и `StateDiff`, когда само состояние не сериализуется в JSON.         |

`AsRandom()` возвращает один адаптер на поток, так что вызов на каждом тике стоит поиска в таблице. Значения берутся из того же потока, что и `ctx.Rng`: для одного seed они одинаковы и никогда не читают системную энтропию.

Код, который читает время, принимает `TimeProvider` вместо `DateTime.UtcNow`, и прогон остаётся детерминированным:

```csharp
using EpochSim;

var report = Epoch.Fuzz<Cache>(f => f
    .State(() => new Cache(TimeSpan.FromSeconds(5)))
    .Step((cache, ctx) =>
    {
        var random = ctx.Rng.AsRandom();
        var now = ctx.TimeProvider.GetUtcNow();
        cache.Get(random.Next(0, 20), now);
        cache.Put(random.Next(0, 20), now);
        if (cache.Count > 15)
            cache.Evict(now);
    })
    .Invariant("NeverServesStaleEntries", cache => cache.StaleHits == 0)
    .CheckDeterminism()
    .Seeds(1, 100)
    .Ticks(200));

Console.WriteLine(report.Failures.Count > 0 ? report.Failures[0].ToText() : report.ToText());

public sealed class Cache(TimeSpan lifetime)
{
    private readonly Dictionary<int, DateTimeOffset> _storedAt = [];

    public int Count => _storedAt.Count;
    public int StaleHits { get; private set; }

    public void Put(int key, DateTimeOffset now) => _storedAt[key] = now;

    public void Get(int key, DateTimeOffset now)
    {
        if (_storedAt.TryGetValue(key, out var storedAt) && now - storedAt > lifetime)
            StaleHits++;
    }

    public void Evict(DateTimeOffset now)
    {
        foreach (var key in _storedAt.Where(entry => now - entry.Value > lifetime).Select(entry => entry.Key).ToList())
            _storedAt.Remove(key);
    }
}
```

Состояние, которое не сериализуется в JSON (циклы, словари с ключами-объектами), заставляет сериализатор по умолчанию бросить исключение. `Project` заменяет его проекцией нужных полей:

```csharp
using EpochSim;

var report = Epoch.Fuzz<Graph>(f => f
    .State(() => new Graph())
    .Step((graph, ctx) => graph.Grow(ctx.Rng.AsRandom()))
    .Project(graph => new { graph.NodeCount, graph.MaxChildren })
    .Invariant("AtMostThreeChildren", graph => graph.MaxChildren <= 3)
    .CheckDeterminism()
    .Seeds(1, 5)
    .Ticks(100));

Console.WriteLine(report.Failures[0].ToText());

public sealed class Graph
{
    private readonly List<Node> _nodes = [];

    public int NodeCount => _nodes.Count;
    public int MaxChildren => _nodes.Count == 0 ? 0 : _nodes.Max(node => node.Children.Count);
    public IReadOnlyList<Node> Nodes => _nodes;

    public void Grow(Random random)
    {
        var parent = _nodes.Count == 0 ? null : _nodes[random.Next(_nodes.Count)];
        var node = new Node(parent);
        parent?.Children.Add(node);
        _nodes.Add(node);
    }
}

public sealed class Node(Node? parent)
{
    public Node? Parent { get; } = parent;
    public List<Node> Children { get; } = [];
}
```

```text
[InvariantViolation] seed=1 tick=6
  AtMostThreeChildren: predicate returned false
  StateDiff:
    $.MaxChildren: 3 -> 4
    $.NodeCount: 6 -> 7
  Repro: .Seeds(1, 1).Ticks(6)
```

`Project` нельзя сочетать с `Serializer`: оба определяют, как сериализуется состояние. Проекция односторонняя: она нужна для fingerprint'ов и диффов, а не для восстановления состояния.

Анализатор детерминизма (правила ESIM001-ESIM006) считает лямбды, переданные в `Step` и `Invariant`, кодом симуляции, как и в `AddSystem`: `new Random()`, `DateTime.Now` и подобное там отмечаются.

## Билдер

| Метод                                     | Обязателен | Смысл                                                                              |
| ----------------------------------------- | ---------- | ---------------------------------------------------------------------------------- |
| `State(Func<TState>)`                     | да         | Фабрика свежего начального состояния. Вызывается для каждого прогона.              |
| `Engine(Action<SimulationEngine<TState>>)` | `Engine` или `Step` | Регистрирует системы и обработчики команд на новом движке. Для каждого прогона. |
| `Step(Action<TState, StepContext>)`       | `Engine` или `Step` | Логика на каждый тик без написания системы. См. выше.                      |
| `Ticks(long)`                             | да         | Последний тик каждого прогона, включительно.                                       |
| `Seeds(ulong from, ulong to)`             | да         | Диапазон seed, включительно.                                                       |
| `Seeds(IEnumerable<ulong>)`               | да         | Явный список seed. Используйте любую из двух перегрузок.                           |
| `Invariant(string, Func<TState, bool>)`   | нет        | Именованный инвариант. Можно вызывать несколько раз.                               |
| `Invariants(Func<IEnumerable<IInvariant<TState>>>)` | нет | Классы инвариантов. Фабрика: у каждого прогона свои экземпляры. Проверяются раньше предикатов `Invariant`. |
| `Serializer(IStateSerializer<TState>)`    | нет        | Для fingerprint'ов и диффа. По умолчанию `JsonStateSerializer<TState>`.            |
| `Project(Func<TState, object>)`           | нет        | Альтернатива `Serializer`: сериализовать проекцию состояния.                       |
| `Commands(Action<FuzzCommandPlan>)`       | нет        | Генерирует входные команды для seed. См. ниже.                                     |
| `Shrink(bool)`                            | нет        | Сокращать команды сбойного seed. По умолчанию `true`, если задан `Commands`.       |
| `OnlyCommands(IReadOnlyList<int>)`        | нет        | Оставить только команды с этими индексами сгенерированного плана.                  |
| `CheckDeterminism(bool)`                  | нет        | Прогнать каждый seed дважды и сравнить fingerprint'ы после каждого тика. По умолчанию `false`. |
| `Name(string)`                            | нет        | Стабильное имя набора в хранилище сбоев. По умолчанию полное имя `TState`.         |
| `FailureStore(string? path = null)`       | нет        | Хранить сбойные seed в файле и запускать их первыми. Путь по умолчанию: `epochsim-failures.json`. |
| `Parallelism(int)`                        | нет        | Число одновременных прогонов. По умолчанию `Environment.ProcessorCount`.           |
| `StopAtFirstFailure(bool)`                | нет        | После первого сбоя не выдавать новые seed. По умолчанию `false`.                   |
| `ArtifactsRoot(string)`                   | нет        | Писать полные артефакты для сбойных seed (не больше первых 10 по возрастанию seed). |
| `EventCodec(IEventCodecV2)`               | нет        | Добавляет лог событий в эти артефакты.                                             |
| `Progress(IProgress<FuzzProgress>)`       | нет        | Вызывается после каждого завершённого seed.                                        |

Неверные значения бросают исключение сразу: нет обязательного параметра, `Seeds` с `from > to`, `Ticks < 0`, `Parallelism < 1`, пустой `Name`, отрицательный индекс в `OnlyCommands`.

## Генерация команд

`Commands` вызывается один раз на seed до начала прогона. `plan.Rng` это детерминированный поток, выведенный из seed. Он отделён от `ctx.Rng`, поэтому генерация команд не сдвигает случайность ваших систем.

```csharp
using EpochSim;
using EpochSim.Kernel.Messaging;

var report = Epoch.Fuzz<Counter>(f => f
    .State(() => new Counter())
    .Engine(e => e.OnCommand<Add>((counter, command, _) => counter.Total += command.Amount))
    .Commands(plan =>
    {
        for (var i = 0; i < 20; i++)
            plan.Enqueue(plan.Rng.NextInt(0, (int)plan.EndTick + 1), new Add(plan.Rng.NextInt(1, 10)));
    })
    .Invariant("TotalBelowLimit", counter => counter.Total < 100)
    .Seeds(1, 10)
    .Ticks(50));

Console.WriteLine(report.Passed);

public sealed class Counter
{
    public int Total { get; set; }
}

public sealed record Add(int Amount) : ICommand;
```

`Enqueue(tick, command)` принимает тики от `0` до `plan.EndTick`. Команды доставляются через `ScheduledCommandInbox`. Генератор должен быть детерминированным: используйте только `plan.Rng` и `plan.Seed`. Индекс команды это её позиция в порядке вызовов `Enqueue`.

## Чтение отчёта

У `FuzzReport` есть `Passed`, `SeedsRun`, `Duration`, `Failures`, `NewFailures`, `KnownFailures`, `FixedSeeds`, `ToText()` и `ThrowIfFailed()`. `Failures` всегда отсортирован по seed, независимо от `Parallelism`.

Каждый `FuzzFailure` содержит `Seed`, `Kind`, `Tick`, `Summary`, `StateDiff` этого тика, `ArtifactsDir` (если задан `ArtifactsRoot`) и `ReproCode`. Также есть `Identity` (имя инварианта или тип исключения) и `Signature`: 16-символьный хэш вида сбоя и `Identity`, одинаковый для всех seed, которые попадают в один и тот же баг.

`StateDiff` перечисляет поля, изменившиеся на сбойном тике: от конца предыдущего тика до сбойного состояния. Для `NonDeterminism` это поля, в которых два прогона различаются. Пути выглядят как `$.Population`, `$.Items[3].Count`, `$["a.b"]`.

## Воспроизведение сбоя

Добавьте `ReproCode` в вызов `Epoch.Fuzz`, чтобы сузить его до сбойного seed:

```text
.Seeds(18, 18).Ticks(19)
```

Прогон падает на том же тике. С `ArtifactsRoot` каталог `fuzz-<seed>` содержит ещё `manifest.json`, `statefp.jsonl`, отчёт о сбое и, при заданном `EventCodec`, лог событий, так что с ним работают `epochsim verify-run`, `bisect` и `repro`.

Если задан `Commands`, `ReproCode` сохраняет заданный `Ticks`, а не тик сбоя: план команд генерируется под заданную длину прогона, и другая длина может дать другой план. Для сокращённого сбоя в нём также есть `OnlyCommands`.

## Шринкинг

У seed с `Commands` могут быть сотни команд, а для сбоя редко нужны все. Когда seed падает, EpochSim сокращает его план команд до минимального набора, который всё ещё падает, методом delta debugging (ddmin). Поддерживаемой .NET-библиотеки для этого нет, поэтому алгоритм входит в EpochSim.

Пример `Account` из [src/EpochSim.Samples/Fuzzing](../../src/EpochSim.Samples/Fuzzing/) генерирует на seed 300 пополнений и снятий и забывает проверять баланс при снятии:

```csharp
using EpochSim;
using EpochSim.Samples.Fuzzing;

var report = Epoch.Fuzz<AccountState>(f => f
    .State(() => new AccountState())
    .Engine(AccountLedger.Register)
    .Invariants(() => [new BalanceNonNegativeInvariant()])
    .Commands(AccountLedger.PlanCommands)
    .Seeds(1, 3)
    .Ticks(500));

Console.WriteLine(report.Failures[0].ToText());
```

```text
[InvariantViolation] seed=1 tick=489
  BalanceNonNegative: Balance=-7
  Commands: 300 -> 2 (shrunk in 13 attempts)
    #4 tick 489: WithdrawCommand
    #5 tick 157: WithdrawCommand
  StateDiff:
    $.Balance: 44 -> -7
  Repro: .Seeds(1, 1).Ticks(500).OnlyCommands([4, 5])
```

Двух команд из 300 достаточно. `StateDiff` относится к минимальному сценарию, а `ReproCode` воспроизводит именно его: `OnlyCommands` оставляет команды с этими индексами сгенерированного плана, поэтому репро это код и сериализация команд не нужна.

Как это работает:

- Кандидат падает так же, если совпадают `Kind` и идентичность (имя инварианта или тип исключения). Тик сбоя может сместиться, это нормально.
- Поиск детерминирован: кандидаты перебираются в фиксированном порядке без случайности, один сбой сокращается в одном потоке. Результат не зависит от `Parallelism`.
- Бюджет: 1000 попыток или 10 секунд на один сбой. Когда он исчерпан, возвращается лучший найденный сценарий. Он всё ещё падает, а отчёт сообщает, что бюджет исчерпан и результат может быть не минимальным.
- Если вы задали `OnlyCommands`, поиск начинается с вашей выборки.

Что не сокращается:

- Без `Commands` сокращать нечего. Seed и тик уже минимальны, потому что прогон останавливается на первом сбое. `OriginalCommandCount` равен `0`, а `MinimalCommandIndices` равен `null`.
- Сбой `NonDeterminism` не сокращается: меньший сценарий нельзя проверить, когда повторные прогоны расходятся. Отчёт говорит об этом (`ShrinkSkipReason`).
- Команды удаляются, но не переносятся на другие тики, и `Ticks` не уменьшается: репро должно выражаться через `OnlyCommands`, а план зависит от `Ticks`.

`Shrink(false)` отключает шринкинг. В CLI используйте `--no-shrink`.

## Хранилище сбоев

Найденный один раз баг должен проверяться всегда: на вашей машине, в CI и у коллег. `FailureStore()` хранит сбойные seed в `epochsim-failures.json`: этот файл предназначен для коммита в git, как `.hypothesis/`:

```csharp
using EpochSim;

var report = Epoch.Fuzz<Inventory>(f => f
    .Name("inventory")
    .State(() => new Inventory())
    .Step((inventory, ctx) => inventory.Tick(ctx.Rng.AsRandom()))
    .Invariant("StockNonNegative", inventory => inventory.Stock >= 0)
    .Seeds(1, 1000)
    .Ticks(500)
    .FailureStore());

Console.WriteLine(report.ToText());

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

Первый запуск создаёт файл. Все 21 сбойных seed попадают в один баг, поэтому они дают одну запись: наименьший seed с его тиком, остальные 20 seed в `otherSeeds` и `signature`. Второй запуск печатает те же сбои с пометкой «известный»:

```text
Fuzz failed: Failures=21 (new=0, known=21), SeedsRun=1000, ElapsedMs=145

Known failures:

[InvariantViolation] seed=18 tick=19 (known)
  StockNonNegative: predicate returned false
  StateDiff:
    $.Stock: 1 -> -2
  Repro: .Seeds(18, 18).Ticks(19)
```

Как ведёт себя хранилище:

- `Name` идентифицирует набор в файле. По умолчанию это полное имя `TState`. Наборы с разными именами не трогают записи друг друга.
- Сохранённые seed набора запускаются первыми, что бы ни говорил `Seeds`. Seed, который есть и в диапазоне, запускается один раз.
- Записи дедуплицируются по `Signature`: вид сбоя плюс имя инварианта или тип исключения. Новый seed с известной сигнатурой добавляется в `otherSeeds`.
- Отчёт разделяет новые сбои и известные. `ThrowIfFailed` бросает в обоих случаях: известный баг остаётся багом.
- Когда проходят все seed записи, она помечается `fixed`, а отчёт один раз пишет `Previously failing seed 18 now passes.`. Записи не удаляются автоматически. Исправленная запись продолжает перепроигрываться; если баг вернётся, она откроется заново и сообщится как новый сбой.
- Seed, который не запускался, например из-за `StopAtFirstFailure`, прошедшим не считается.
- Запись атомарна (временный файл, затем перемещение) и сериализована между потоками и процессами, поэтому параллельные прогоны тестов не портят файл. Файл отсортирован, так что диффы в git остаются небольшими.
- У файла есть `version`. Файл, записанный более новым EpochSim, отклоняется с сообщением, а не читается неверно. Файл, который не является корректным JSON, отклоняется и никогда не перезаписывается.
- Без `FailureStore` ничего не читается и не пишется.

С файлом работает команда `failures`:

```bash
epochsim failures list
epochsim failures prune
epochsim failures export-test --framework xunit --configure MyApp.InventoryFuzz.Configure --out tests/
```

- `list` печатает все записи, `prune` удаляет исправленные.
- `export-test` пишет по одному регрессионному тесту на запись для xunit, nunit или mstest. Конфигурацию симуляции с лямбдами сгенерировать нельзя, поэтому `--configure` указывает на метод `public static void Configure(FuzzBuilder<TState>)`, который строит ту же симуляцию, что и ваш вызов `Epoch.Fuzz`, а тесты вызывают его. Если в хранилище несколько наборов, укажите `--name`. Тип состояния берётся из хранилища; `--state-type` переопределяет его.

Для хранилища выше сгенерированный `InventoryFuzzRegressionTests.cs` выглядит так:

```text
using EpochSim.Testing;
using Xunit;

namespace MyApp;

public sealed class InventoryFuzzRegressionTests
{
    [Fact]
    public void Seed18_StockNonNegative_AtTick19()
    {
        SimAssert.Regression<global::EpochSim.Samples.Onboarding.Inventory>(global::MyApp.InventoryFuzz.Configure, 18, 19);
    }
}
```

Тест падает, пока баг не исправлен, и после исправления продолжает его охранять. `SimAssert.Regression` и `SimAssert.StoredFailuresStayFixed` описаны в [Testing](testing.md).

## Проверка детерминизма

`CheckDeterminism(true)` прогоняет каждый seed дважды и сравнивает fingerprint состояния после каждого тика. Ловит `static`-состояние, `DateTime.UtcNow`, `Random`, зависимость от порядка хэш-коллекций и всё остальное, из-за чего два прогона одного seed различаются. Прогоны не делят изменяемое состояние, кроме того, что делит ваш код, например `static`-полей: это и находит проверка.

## Параллелизм

Seed выполняются параллельно, один прогон на поток. Отчёт не зависит от `Parallelism`. `StopAtFirstFailure(true)` прекращает выдачу новых seed, но уже запущенные прогоны завершаются и их сбои попадают в отчёт. Какие seed были запущены, зависит от тайминга, поэтому набор сбоев стабилен только при `Parallelism(1)`.

## Командная строка

```bash
epochsim fuzz --adapter fuzzbug --seeds 1..1000 --ticks 500
```

| Опция                 | По умолчанию | Смысл                                           |
| --------------------- | ------------ | ----------------------------------------------- |
| `--adapter name`      | `population` | Доменный адаптер, см. `epochsim list-adapters`  |
| `--seeds A..B`        | `1..1000`    | Диапазон seed или один seed `N`                 |
| `--ticks N`           | `500`        | Последний тик каждого прогона                   |
| `--check-determinism` | выкл.        | Прогнать каждый seed дважды                     |
| `--parallel N`        | число CPU    | Число одновременных прогонов                    |
| `--out dir`           | `artifacts`  | Каталог артефактов для сбойных seed             |
| `--no-shrink`         | выкл.        | Оставить полный план команд сбойного seed       |
| `--store path`        | нет          | Записывать сбои в хранилище, имя набора совпадает с адаптером |

Код возврата `0`: сбоев нет. Код `2`: найдены сбои или неверные аргументы. CLI работает с адаптерами из `DomainAdapterRegistry`; в собственном коде основной вход это библиотечный API. `epochsim init` создаёт стартовый проект, в котором уже есть вызов `Epoch.Fuzz`.

## Запуск в CI

Вызывайте `report.ThrowIfFailed()` из теста или используйте `SimAssert` из [Testing](testing.md), с большим диапазоном `Seeds` и `.ArtifactsRoot("fuzz-artifacts")`. Если тест упал, загрузите `fuzz-artifacts` как артефакт сборки. Коммитьте `epochsim-failures.json`: тогда CI перепроигрывает seed, упавшие у вас, и наоборот.
