# Встраивание EpochSim

Основной пакет для подключения: `EpochSim.All` (метапакет).
API билдера запуска находится в namespace `EpochSim.Hosting`.

Установка:

```bash
dotnet add package EpochSim.All
```

## Базовый процесс

1. Настроить `SimulationEngine<TState>` (системы + command handlers).
2. Собрать `IEventCodecV2` через `JsonEventCodecBuilder`.
3. Собрать `EpochSimRunScope<TState>` с нужными middleware.
4. Подключить scope к движку и запустить симуляцию.
5. Освободить scope (`Dispose`) для flush и финализации метаданных запуска.

## Минимальный пример

```csharp
var state = new WorldState();
var engine = new SimulationEngine<WorldState>();

var serializer = new JsonStateSerializer<WorldState>();
var codec = new JsonEventCodecBuilder()
    .Register<MyEvent>()
    .Build();

using var run = EpochSimRun.For(state)
    .WithRootDirectory("artifacts")
    .WithRunId(RunId.New())
    .WithEventLog(codec)
    .WithTraceJsonl()
    .WithStateFingerprints(serializer)
    .Build();

engine.Attach(run);
engine.RunTicks(state, seed: 12345, start: SimTime.Zero, endInclusive: new SimTime(100), context: run.Context);
```

## Связанные разделы

- Имена/форматы артефактов: `docs/Artifacts.ru.md`
- Долгоживущие сессии: `docs/Sessions.ru.md`
- Правила детерминизма и типовые ошибки: `docs/Determinism.ru.md`
