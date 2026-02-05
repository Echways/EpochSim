# EpochSim

Детерминированный движок тиковых симуляций на C# с журналом событий, воспроизведением, снапшотами и инструментами анализа.

## Что это
EpochSim — библиотека и набор инструментов для симуляций, где важна воспроизводимость.  
Один и тот же вход, seed и диапазон тиков всегда дают идентичный результат.

## Когда подходит
EpochSim полезен, если нужно:
- гарантировать повторяемость результатов;
- логировать события и воспроизводить прогоны;
- быстро перематывать состояние с помощью снапшотов;
- получать удобные артефакты для анализа и тестов.

## Установка (NuGet)
Самый простой вариант — метапакет:
```bash
dotnet add package EpochSim.All
```

Если нужен более тонкий контроль, используйте отдельные пакеты:
- `EpochSim.Kernel` — время, события, планировщик, RNG
- `EpochSim.Execution` — движок и middleware
- `EpochSim.Serialization` — event log, снапшоты, сериализация
- `EpochSim.Observability` — трассировка

Минимальный набор для запуска симуляции: `EpochSim.Kernel` + `EpochSim.Execution`.  
Для логов и воспроизведения добавьте `EpochSim.Serialization`, для трассировки — `EpochSim.Observability`.

CLI как .NET tool:
```bash
dotnet tool install -g EpochSim.Cli
epochsim --help
```

`EpochSim.Samples` — демо‑домен Population, на NuGet не публикуется.

## Быстрый старт (встраивание в свой проект)
Минимальный сценарий: определяем состояние, команды/события и систему, затем запускаем движок.

```csharp
public sealed class WorldState
{
    public int Value { get; set; }
}

public sealed record IncCommand(int Delta) : ICommand { public string Kind => "Inc"; }
public sealed record ValueChanged(int Delta) : IEvent { public string Kind => "ValueChanged"; }

public sealed class IncHandler : ICommandHandler<WorldState, IncCommand>
{
    public void Handle(WorldState state, IncCommand command, IEventBuffer events)
        => events.Emit(new ValueChanged(command.Delta));
}

public sealed class WorldSystem : ISystem<WorldState>
{
    public string Name => "world";

    public void Tick(TickContext<WorldState> ctx)
    {
        if (ctx.Time.Tick == 0)
            ctx.Commands.Enqueue(new IncCommand(5));
    }

    public void Handle(EventContext<WorldState> ctx, IEvent ev)
    {
        if (ev is ValueChanged e)
            ctx.State.Value += e.Delta;
    }
}
```

Запуск:
```csharp
var engine = new SimulationEngine<WorldState>();
engine.AddSystem(new WorldSystem());
engine.RegisterCommandHandler(new IncHandler());

var state = new WorldState();
engine.RunTicks(state, seed: 12345, start: SimTime.Zero, endInclusive: new SimTime(100));
```

## Быстрый старт (CLI)
CLI не зависит от конкретного домена — он работает через `IDomainAdapter`.  
В репозитории есть демо‑домен Population; примеры ниже используют его.

Запуск симуляции и запись артефактов в `artifacts/`:
```bash
dotnet run --project src/EpochSim.Cli run artifacts
```

Валидация (инварианты + дампы при нарушении):
```bash
dotnet run --project src/EpochSim.Cli validate-run artifacts
```

Проверка детерминизма по state fingerprints:
```bash
dotnet run --project src/EpochSim.Cli verify-run artifacts
```

Быстрое воспроизведение с использованием снапшотов:
```bash
dotnet run --project src/EpochSim.Cli fast-replay artifacts
```

Сводка по событиям:
```bash
dotnet run --project src/EpochSim.Cli event-stats artifacts
```

Таймлайн событий за диапазон тиков:
```bash
dotnet run --project src/EpochSim.Cli timeline artifacts 0 100
```

Инспект артефактов:
```bash
dotnet run --project src/EpochSim.Cli inspect-run artifacts <runId>
dotnet run --project src/EpochSim.Cli pretty-inspect artifacts <runId>
```

Бисект первого нарушения инварианта:
```bash
dotnet run --project src/EpochSim.Cli bisect artifacts <runId>
```

## Что создаёт запуск
В каталоге `artifacts/<runId>/`:
- `events.jsonl` / `events.jsonl.gz` — журнал событий (JSONL, v2).
- `trace.jsonl` / `trace.jsonl.gz` — трассировка.
- `statefp.jsonl` — отпечатки состояния (SHA‑256 от канонического JSON).
- `snapshots/` — снапшоты состояния.
- `dumps/` — дампы при нарушениях инвариантов.
- `manifest.json` — манифест запуска (версии, опции, интервалы).
- `meta.txt` — старый мета‑файл (для совместимости).
- `failure-report.json` и `failure-snapshot.json` — отчёт о падении (если ошибка во время прогона).

## Основные правила
- **Детерминизм:** одинаковые входные данные дают идентичный результат.
- **Тики:** `OnTickStart(T)` вызывается до любого диспетча событий тика `T`.  
  `OnTickEnd(T)` — после завершения обработки команд/событий.
- **Очереди:** команды и события обрабатываются FIFO.
- **Планировщик:** `ScheduleAt` разрешён только на будущий тик (`targetTick > currentTick`).  
  Для удобства есть `ScheduleNextTick` и `ScheduleInTicks(>=1)`.
- **Немедленные события:** `ctx.Events.Emit(ev)` добавляет событие в текущий тик (в рамках pump‑цикла).
- **Replay:** в strict‑режиме `Emit` запрещён.

Подробно: [DeterminismContract](DeterminismContract.md).

## Воспроизведение
```csharp
var codec = /* IEventCodecV2 */;
var entries = EventLogReader.ReadStream("events.jsonl");

engine.ReplayFromLogStream(
    state,
    seed: 12345,
    start: SimTime.Zero,
    endInclusive: new SimTime(100),
    entries: entries,
    codec: codec);
```

## Опции запуска
В `RunOptions` можно ограничивать pump‑цикл:
- `MaxPumpStepsPerTick` — защита от бесконечного цикла команд/событий.
- `MaxEventsPerTick` — лимит событий на тик.
- `RngVersion` — версия генератора (`V1` для совместимости, `V2` по умолчанию).

В CLI:
```bash
--max-pump-steps N
--max-events-per-tick N
--snapshot-every N
--fingerprint-every N
--rng-version v1|v2
--compress
--cancel-after-ms N
```

## Доменный адаптер CLI
CLI не зависит от конкретного домена — он работает через `IDomainAdapter`.
Минимальный набор:
- `CreateInitialState()`
- `CreateSystems()` / `ConfigureEngine()`
- `Codec`, `Serializer`
- `CreateInvariants()` (если нужны)

См. пример: [PopulationDomainAdapter.cs](src/EpochSim.Cli/Domain/PopulationDomainAdapter.cs).

## Тесты и бенчмарки
Тесты:
```bash
dotnet test tests/EpochSim.Determinism.Tests/EpochSim.Determinism.Tests.csproj
```

Бенчмарки:
```bash
dotnet run -c Release --project benchmarks/EpochSim.Benchmarks
```

## Структура репозитория
- `src/EpochSim.All` — метапакет (NuGet)
- `src/EpochSim.Kernel` — ядро: время, события, планировщик, RNG
- `src/EpochSim.Execution` — движок и middleware
- `src/EpochSim.Serialization` — event log, снапшоты, сериализация
- `src/EpochSim.Observability` — трассировка
- `src/EpochSim.Samples` — демо‑домен Population (не публикуется)
- `src/EpochSim.Cli` — CLI‑инструменты
- `tests/` — детерминизм и семантика
- `benchmarks/` — BenchmarkDotNet
