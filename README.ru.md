<div align="center">

# EpochSim

**Найдите seed, на котором ломается ваша симуляция.**
Детерминированный движок тиковых симуляций для .NET 10: фаззинг по seed, воспроизведение любого прогона и бисект до первого расходящегося тика.

[English](README.md) · **Русский**

[![NuGet](https://img.shields.io/nuget/v/EpochSim)](https://www.nuget.org/packages/EpochSim)
[![CI](https://github.com/Echways/EpochSim/actions/workflows/ci.yml/badge.svg)](https://github.com/Echways/EpochSim/actions/workflows/ci.yml)
[![License](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

</div>

Симуляцию, которую нельзя воспроизвести, нельзя отладить. EpochSim фиксирует входы каждого тика: один и тот же seed и те же команды всегда дают то же состояние, а расхождение локализуется до одного тика.

- **Фаззинг по seed.** `Epoch.Fuzz` прогоняет ваш код на тысячах seed, называет сбойный seed и тик и показывает, какие поля состояния изменились. Сбои сокращаются до минимального сценария команд.
- **Детерминизм по построению.** Прогон определяется состоянием, seed и упорядоченным входом. Системы выполняются в фиксированном порядке в одном потоке симуляции.
- **Replay и bisect.** Проверка прогона по записанным fingerprint'ам и поиск первого тика, на котором нарушается инвариант.
- **Любой тестовый фреймворк.** `SimAssert` работает в xUnit, NUnit и MSTest и превращает найденные баги в регрессионные тесты.
- **Roslyn-анализатор.** Находит в коде симуляции системное время, `Random`, потоки и другие источники недетерминизма.
- **CLI.** Запуск, инспекция, проверка, replay, bisect и фаззинг из терминала.

## Установка

```bash
dotnet add package EpochSim
dotnet add package EpochSim.Testing
dotnet tool install --global EpochSim.Cli
```

Требуется .NET 10. `EpochSim.All` приносит весь граф модулей одной зависимостью.

## Найдите баг по seed

`Epoch.Fuzz` не требует переписывать существующий код. Вызовите его из `Step`, опишите в `Invariant`, что должно выполняться всегда, и задайте seed.

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

Прогон находит seed, на которых остаток уходит в минус, и называет тик и изменившиеся поля состояния:

```text
Fuzz failed: Failures=21, SeedsRun=1000, ElapsedMs=150

[InvariantViolation] seed=18 tick=19
  StockNonNegative: predicate returned false
  StateDiff:
    $.Stock: 1 -> -2
  Repro: .Seeds(18, 18).Ticks(19)

...
```

Добавьте `.Seeds(18, 18).Ticks(19)` из строки `Repro` в свой вызов, чтобы прогнать только этот сбой. Подробнее в [Fuzzing](docs/ru/fuzzing.md).

## Запуск симуляции

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

Пресеты, внешний ввод, сессии и артефакты описаны в [Embedding](docs/ru/embedding.md).

## Тесты

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

## Командная строка

```bash
epochsim fuzz --adapter fuzzbug --seeds 1..1000
epochsim verify-run artifacts <runId>
epochsim bisect artifacts <runId>
epochsim failures list
```

## Документация

| Руководство                            | Содержание                                          |
| -------------------------------------- | --------------------------------------------------- |
| [Embedding](docs/ru/embedding.md)      | Пресеты, хосты, серверы, внешний ввод               |
| [Fuzzing](docs/ru/fuzzing.md)          | Seed, шринкинг, хранилище сбоев, CLI, CI            |
| [Testing](docs/ru/testing.md)          | `SimAssert`, золотые fingerprint'ы, регрессионные тесты |
| [Determinism](docs/ru/determinism.md)  | Правила и анализатор ESIM001-ESIM006                |
| [Sessions](docs/ru/sessions.md)        | Долгоживущие циклы и пошаговый режим                |
| [Artifacts](docs/ru/artifacts.md)      | Структура каталога прогона и форматы файлов         |

Запускаемые примеры лежат в [src/EpochSim.Samples](src/EpochSim.Samples).

## Разработка

```bash
dotnet build EpochSim.slnx -m:1
dotnet test EpochSim.slnx -m:1
```

## Лицензия

[MIT](LICENSE)
