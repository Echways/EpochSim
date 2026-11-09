# Testing

`EpochSim.Testing` добавляет проверки симуляции в обычные юнит-тесты. Пакет не зависит ни от одного тестового фреймворка: упавшая проверка бросает `SimulationAssertionException`, а любой фреймворк считает необработанное исключение провалом теста. Внутри работает [`Epoch.Fuzz`](fuzzing.md), поэтому сообщение о сбое называет seed, тик и изменившиеся поля состояния.

## Установка

```bash
dotnet add package EpochSim.Testing
```

Это тестовая зависимость, в `EpochSim.All` она не входит.

## Проверки

| Метод                                               | Что проверяет                                                          |
| --------------------------------------------------- | ---------------------------------------------------------------------- |
| `SimAssert.IsDeterministic<TState>(configure)`      | Прогоняет каждый seed дважды и сравнивает состояние после каждого тика. Всегда включает `CheckDeterminism`. |
| `SimAssert.InvariantsHold<TState>(configure)`       | Инварианты, исключения и, если `configure` включает, детерминизм.      |
| `SimAssert.NoFailures(report)`                      | Бросает исключение, если в `FuzzReport` есть сбои.                     |
| `SimAssert.MatchesGolden<TState>(configure, path, seed)` | Сравнивает fingerprint'ы одного seed с сохранённым файлом.        |

`configure` это тот же билдер, что в `Epoch.Fuzz`. Один seed записывается как `Seeds(s, s)`.

## xUnit

```csharp
using EpochSim.Testing;
using Xunit;

public sealed class WorldTests
{
    [Fact]
    public void World_IsDeterministic()
    {
        SimAssert.IsDeterministic<WorldState>(f => f
            .State(() => new WorldState())
            .Engine(e => e.AddSystem("World", tick: ctx => ctx.State.Population++))
            .Seeds(1, 50)
            .Ticks(200));
    }
}
```

## NUnit

```csharp
using EpochSim.Testing;
using NUnit.Framework;

public sealed class WorldTests
{
    [Test]
    public void World_IsDeterministic()
    {
        SimAssert.IsDeterministic<WorldState>(f => f
            .State(() => new WorldState())
            .Engine(e => e.AddSystem("World", tick: ctx => ctx.State.Population++))
            .Seeds(1, 50)
            .Ticks(200));
    }
}
```

## MSTest

```csharp
using EpochSim.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class WorldTests
{
    [TestMethod]
    public void World_IsDeterministic()
    {
        SimAssert.IsDeterministic<WorldState>(f => f
            .State(() => new WorldState())
            .Engine(e => e.AddSystem("World", tick: ctx => ctx.State.Population++))
            .Seeds(1, 50)
            .Ticks(200));
    }
}
```

## Как читать сообщение об ошибке

```
Fuzz failed: Failures=1, SeedsRun=50, ElapsedMs=12

[InvariantViolation] seed=4 tick=426
  QueueNonNegative: Pending=-1
  StateDiff:
    $.Completed: 198 -> 200
    $.Pending: 0 -> -1
  Repro: .Seeds(4, 4).Ticks(426)
```

Первая строка это итоги. В каждом блоке: вид сбоя, seed, тик, что сломалось, поля состояния, изменившиеся на этом тике, и фрагмент `Repro:`, который сужает тот же тест до сбойного seed.

## Золотые fingerprint'ы

`MatchesGolden` это регрессионный тест на случай «я случайно изменил поведение»:

```csharp
SimAssert.MatchesGolden<WorldState>(
    f => f
        .State(() => new WorldState())
        .Engine(e => e.AddSystem(new WorldSystem()))
        .Ticks(1000),
    goldenPath: "golden/world-seed-7.json",
    seed: 7);
```

Первый запуск создаёт файл. В нём fingerprint'ы состояния этого seed на каждом сотом тике и на последнем. Следующие запуски сравниваются с файлом и называют первый тик, на котором есть расхождение. После намеренного изменения запустите тесты один раз с `EPOCHSIM_UPDATE_GOLDEN=1`, чтобы перезаписать файл, и закоммитьте его.
