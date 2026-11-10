# Fuzzing

`Epoch.Fuzz` прогоняет симуляцию на диапазоне seed и показывает, какой seed её ломает, на каком тике и какие поля состояния изменились на этом тике. Находит три вида сбоев:

- `InvariantViolation`: `IInvariant<TState>` вернул `false`.
- `Exception`: исключение из системы или обработчика команды.
- `NonDeterminism`: два прогона одного seed дали разное состояние (нужен `CheckDeterminism`).

Любой сбой воспроизводится тем же seed.

## Минимальный пример

```csharp
using EpochSim;

var report = Epoch.Fuzz<JobQueueState>(f => f
    .State(() => new JobQueueState())
    .Engine(e => e.AddSystem(new JobQueueSystem()))
    .Invariants(() => [new QueueNonNegativeInvariant()])
    .Seeds(1, 1000)
    .Ticks(500));

Console.WriteLine(report.ToText());
```

Вывод:

```
Fuzz failed: Failures=14, SeedsRun=1000, ElapsedMs=81

[InvariantViolation] seed=4 tick=426
  QueueNonNegative: Pending=-1
  StateDiff:
    $.Completed: 198 -> 200
    $.Pending: 0 -> -1
  Repro: .Seeds(4, 4).Ticks(426)
```

В тесте вызывайте `report.ThrowIfFailed()` или используйте [EpochSim.Testing](testing.md).

Рабочий пример лежит в [src/EpochSim.Samples/Fuzzing](../../src/EpochSim.Samples/Fuzzing/): очередь задач, счётчик которой уходит в минус на редкой последовательности значений `ctx.Rng`, зависящей от seed.

## Билдер

| Метод                                     | Обязателен | Смысл                                                                              |
| ----------------------------------------- | ---------- | ---------------------------------------------------------------------------------- |
| `State(Func<TState>)`                     | да         | Фабрика свежего начального состояния. Вызывается для каждого прогона.              |
| `Engine(Action<SimulationEngine<TState>>)` | да        | Регистрирует системы и обработчики команд на новом движке. Для каждого прогона.    |
| `Ticks(long)`                             | да         | Последний тик каждого прогона, включительно.                                       |
| `Seeds(ulong from, ulong to)`             | да         | Диапазон seed, включительно.                                                       |
| `Seeds(IEnumerable<ulong>)`               | да         | Явный список seed. Используйте любую из двух перегрузок.                           |
| `Serializer(IStateSerializer<TState>)`    | нет        | Для fingerprint'ов и диффа. По умолчанию `JsonStateSerializer<TState>`.            |
| `Invariants(Func<IEnumerable<IInvariant<TState>>>)` | нет | Проверяются после каждого тика. Фабрика: у каждого прогона свои экземпляры. |
| `Commands(Action<FuzzCommandPlan>)`       | нет        | Генерирует входные команды для seed. См. ниже.                                     |
| `CheckDeterminism(bool)`                  | нет        | Прогнать каждый seed дважды и сравнить fingerprint'ы после каждого тика. По умолчанию `false`. |
| `Parallelism(int)`                        | нет        | Число одновременных прогонов. По умолчанию `Environment.ProcessorCount`.           |
| `StopAtFirstFailure(bool)`                | нет        | После первого сбоя не выдавать новые seed. По умолчанию `false`.                   |
| `ArtifactsRoot(string)`                   | нет        | Писать полные артефакты для сбойных seed (не больше первых 10 по возрастанию seed). |
| `EventCodec(IEventCodecV2)`               | нет        | Добавляет лог событий в эти артефакты.                                             |
| `Progress(IProgress<FuzzProgress>)`       | нет        | Вызывается после каждого завершённого seed.                                        |

Неверные значения бросают исключение сразу: нет обязательного параметра, `Seeds` с `from > to`, `Ticks < 0`, `Parallelism < 1`.

## Генерация команд

`Commands` вызывается один раз на seed до начала прогона. `plan.Rng` это детерминированный поток, выведенный из seed. Он отделён от `ctx.Rng`, поэтому генерация команд не сдвигает случайность ваших систем.

```csharp
.Commands(plan =>
{
    for (var i = 0; i < 20; i++)
        plan.Enqueue(plan.Rng.NextInt(0, (int)plan.EndTick), new AddPopulation(plan.Rng.NextInt(1, 10)));
})
```

`Enqueue(tick, command)` принимает тики от `0` до `plan.EndTick`. Команды доставляются через `ScheduledCommandInbox`.

## Чтение отчёта

У `FuzzReport` есть `Passed`, `SeedsRun`, `Duration`, `Failures`, `ToText()` и `ThrowIfFailed()`. `Failures` всегда отсортирован по seed, независимо от `Parallelism`.

Каждый `FuzzFailure` содержит `Seed`, `Kind`, `Tick`, `Summary`, `StateDiff` этого тика, `ArtifactsDir` (если задан `ArtifactsRoot`) и `ReproCode`.

`StateDiff` перечисляет поля, изменившиеся на сбойном тике: от конца предыдущего тика до сбойного состояния. Для `NonDeterminism` это поля, в которых два прогона различаются. Пути выглядят как `$.Population`, `$.Items[3].Count`, `$["a.b"]`.

## Воспроизведение сбоя

Добавьте `ReproCode` в вызов `Epoch.Fuzz`, чтобы сузить его до сбойного seed:

```csharp
.Seeds(4, 4).Ticks(426)
```

Прогон падает на том же тике. С `ArtifactsRoot` каталог `fuzz-<seed>` содержит ещё `manifest.json`, `statefp.jsonl`, отчёт о сбое и, при заданном `EventCodec`, лог событий, так что с ним работают `epochsim verify-run`, `bisect` и `repro`.

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

Код возврата `0`: сбоев нет. Код `2`: найдены сбои или неверные аргументы. CLI работает с адаптерами из `DomainAdapterRegistry`; в собственном коде основной вход это библиотечный API.

## Запуск в CI

```csharp
[Fact]
public void World_SurvivesFuzzing()
{
    Epoch.Fuzz<WorldState>(f => f
        .State(() => new WorldState())
        .Engine(e => e.AddSystem(new WorldSystem()))
        .Invariants(() => [new PopulationNonNegativeInvariant()])
        .Seeds(1, 2000)
        .Ticks(500)
        .ArtifactsRoot("fuzz-artifacts")
        .CheckDeterminism()).ThrowIfFailed();
}
```

Если тест упал, загрузите `fuzz-artifacts` как артефакт сборки.
