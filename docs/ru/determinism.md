# Детерминизм

EpochSim гарантирует детерминированное выполнение при одинаковых входных данных: начальное состояние, seed, системы, команды и события, опции. Системы и обработчики не должны добавлять другие источники вариативности.

## Частые ошибки

- `DateTime.Now`, `DateTime.UtcNow`
- `Random` вместо `ctx.Rng`
- `Guid.NewGuid()` в логике симуляции
- порядок обхода hash-коллекций
- гонки потоков, меняющие порядок событий
- неконтролируемое поведение с плавающей точкой

## Анализатор

В `EpochSim` входит Roslyn-анализатор, превращающий эти ошибки в предупреждения компилятора. Он лежит в пакете `EpochSim` и доступен отдельно как `EpochSim.Analyzers`. Анализатор срабатывает только внутри кода симуляции и не трогает остальной проект.

### Что считается кодом симуляции

- Лямбды и методовые группы, переданные в `SimulationEngine<TState>.AddSystem(...)` и `OnCommand(...)`.
- Все методы типов, реализующих `ISystem<>`, `ICommandHandler<,>` или `IInvariant<>`.
- Типы и методы с атрибутом `[Deterministic]` (`EpochSim.Kernel.Determinism`).

Вызовы в другие методы не отслеживаются. Пометьте вспомогательный метод `[Deterministic]`, чтобы проверить и его.

<!-- snippet: compiles -->
```csharp
using EpochSim.Kernel.Determinism;

[Deterministic]
public static class Pricing
{
    public static int Roll(IRng rng) => rng.NextInt(1, 7);
}
```

### Правила

| ID      | Серьёзность по умолчанию | Что ловит                                                                                                          |
| ------- | ------------------------ | ------------------------------------------------------------------------------------------------------------------ |
| ESIM001 | warning                  | `DateTime.Now/UtcNow/Today`, `DateTimeOffset.Now/UtcNow`, `Stopwatch`, `Environment.TickCount/TickCount64`, `TimeProvider.System` |
| ESIM002 | warning                  | `new Random(...)`, `Random.Shared`, `RandomNumberGenerator`, `Guid.NewGuid()`                                      |
| ESIM003 | warning                  | `Task.Run`, `Task.Factory.StartNew`, `new Thread`, `ThreadPool.*`, `Parallel.*`, `AsParallel()`, `async`-лямбды, `await` |
| ESIM004 | warning                  | `GetHashCode()` у `string` и `object`, перебор `ConcurrentDictionary`, `ConcurrentBag`, `ConcurrentQueue`          |
| ESIM005 | warning                  | `File.*`, `Directory.*`, `HttpClient`, `Environment.GetEnvironmentVariable`, `Environment.MachineName`             |
| ESIM006 | hidden                   | `foreach` и LINQ по `Dictionary<,>` и `HashSet<>`                                                                  |

ESIM006 скрыто по умолчанию: .NET перебирает эти коллекции в стабильном порядке при одинаковых операциях, поэтому код хрупкий, но не неверный. Повышайте серьёзность, когда нужна более строгая проверка.

### Настройка

Через `.editorconfig`:

```ini
[*.cs]
dotnet_diagnostic.ESIM001.severity = error
dotnet_diagnostic.ESIM005.severity = none
dotnet_diagnostic.ESIM006.severity = warning
```

Отдельное срабатывание подавляется `#pragma warning disable ESIM001`.

### Исправление

ESIM002 предлагает исправление, когда в области видимости есть параметр `TickContext<>` или `EventContext<>`:

| До                                  | После                      |
| ----------------------------------- | -------------------------- |
| `Random.Shared.Next(a, b)`          | `ctx.Rng.NextInt(a, b)`    |
| `new Random(seed).Next(a, b)`       | `ctx.Rng.NextInt(a, b)`    |
| `Random.Shared.NextDouble()`        | `ctx.Rng.NextDouble01()`   |

Проверка методовых групп выполняется при анализе всей компиляции (при сборке). В IDE она может появиться только после сборки.

## Рекомендации

- Используйте только `ctx.Rng` для доменной случайности.
- Держите обработчики команд и событий явными, с контролируемыми побочными эффектами.
- Сохраняйте `statefp.jsonl` для сравнения прогонов.
- Включайте снимки для быстрого детерминированного replay.

## Правила

Любое отклонение от этих правил считается регрессией корректности.

### 1. Жизненный цикл прогона

- `OnRunStart` вызывается ровно один раз до начала обработки тиков.
- `OnRunEnd` вызывается ровно один раз, в том числе при ошибке.
- При исключении `OnRunFailed` вызывается один раз перед повторным выбросом исключения.

### 2. Порядок тиков

- Для каждого тика `T` первым вызывается `OnTickStart(T)`.
- События тика `T` не диспетчатся до `OnTickStart(T)`.
- `OnTickEnd(T)` вызывается после завершения обработки команд и событий.

### 3. Порядок систем

- Системы тикаются в порядке регистрации.
- `OnSystemTickStart(T, system)` и `OnSystemTickEnd(T, system)` обрамляют каждый вызов `Tick`.
- Обработка событий (`Handle`) тоже идёт в порядке регистрации систем.

### 4. Планировщик

- За тик `T` обрабатываются только события, запланированные на `T`.
- События одного тика обрабатываются в порядке добавления.
- События за пределами `endTickInclusive` не диспетчатся.

### 5. Цикл команд и событий

Внутри каждого тика движок повторяет шаги, пока обе очереди не пусты:

1. Забирает и обрабатывает все команды в порядке FIFO, добавляя события в очередь.
2. Забирает и обрабатывает все события в порядке FIFO для всех систем в порядке регистрации.

### 6. Middleware

- Callbacks middleware вызываются в порядке регистрации для каждого hook.
- Последовательность `OnRunStart`, тики, `OnRunEnd` стабильна.
- `OnRunFailed` вызывается ровно один раз.
