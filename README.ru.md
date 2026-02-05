# EpochSim

Детерминированный движок тиковых симуляций для .NET 10.

`EpochSim.All` — рекомендуемый пакет для встраивания. Он подтягивает Kernel, Execution, Hosting, Serialization и Observability.
English version: [README.md](https://github.com/Echways/EpochSim/blob/main/README.md)

## Установка

```bash
dotnet add package EpochSim.All
```

Опционально можно ставить пакеты по отдельности:
- `EpochSim.Kernel`
- `EpochSim.Execution`
- `EpochSim.Hosting`
- `EpochSim.Serialization`
- `EpochSim.Observability`

## Путь внедрения (10-15 минут)

### 1) Определите состояние, сообщения, систему и обработчик команд

```csharp
using EpochSim.Kernel.Messaging;
using EpochSim.Kernel.Systems;
using EpochSim.Kernel.Time;
using EpochSim.Kernel.Validation;

public sealed class WorldState
{
    public int Population { get; set; }
}

[MessageKind("Grow")]
public sealed record GrowCommand(int Delta) : ICommand;

public sealed record PopulationChanged(int Delta) : IEvent;

public sealed class WorldSystem : ISystem<WorldState>
{
    public string Name => "World";

    public void Tick(TickContext<WorldState> ctx)
    {
        if (ctx.Time.Tick % 10 == 0)
            ctx.Commands.Enqueue(new GrowCommand(1));
    }

    public void Handle(EventContext<WorldState> ctx, IEvent ev)
    {
        if (ev is PopulationChanged changed)
            ctx.State.Population += changed.Delta;
    }
}

public sealed class GrowHandler : ICommandHandler<WorldState, GrowCommand>
{
    public void Handle(WorldState state, GrowCommand command, IEventBuffer events)
        => events.Emit(new PopulationChanged(command.Delta));
}

public sealed class PopulationNonNegativeInvariant : IInvariant<WorldState>
{
    public string Name => "Population >= 0";

    public bool Check(SimTime time, WorldState state, out string message)
    {
        var ok = state.Population >= 0;
        message = ok ? "" : $"Population is negative at tick {time.Tick}.";
        return ok;
    }
}
```

Примечания:
- Для `IEvent` и `ICommand` больше не нужно вручную писать `Kind`.
- По умолчанию `Kind` = имя типа (`PopulationChanged`), а для стабильного публичного имени используйте `[MessageKind("...")]`.

### 2) Настройте движок и JSON-кодек

```csharp
using EpochSim.Execution;
using EpochSim.Serialization.EventLog;

var state = new WorldState();
var engine = new SimulationEngine<WorldState>();

engine.AddSystem(new WorldSystem());
engine.RegisterCommandHandler(new GrowHandler());

var codec = new JsonEventCodecBuilder()
    .Register<PopulationChanged>()
    .WithStrictUnknownKinds(strict: true)
    .Build();
```

### 3) Соберите scope запуска с артефактами и подключите к движку

```csharp
using EpochSim.Execution.RunArtifacts;
using EpochSim.Hosting;
using EpochSim.Kernel.Time;
using EpochSim.Kernel.Validation;
using EpochSim.Serialization.State;

var serializer = new JsonStateSerializer<WorldState>();
IInvariant<WorldState>[] invariants = [new PopulationNonNegativeInvariant()];

using var run = EpochSimRun.For(state)
    .WithRootDirectory("artifacts")
    .WithRunId(RunId.New())
    .WithCompression(true)
    .WithEventLog(codec)
    .WithSnapshots(serializer, everyTicks: 50)
    .WithStateFingerprints(serializer, everyTicks: 1)
    .WithTraceJsonl()
    .WithProfilingJsonl()
    .WithInvariants(invariants, checkEveryTicks: 1)
    .WithStateMutationGuard(serializer)
    .WithFailureArtifacts(serializer, codec, tailSize: 200)
    .Build();

engine.Attach(run);
engine.RunTicks(
    state,
    seed: 12345,
    start: SimTime.Zero,
    endInclusive: new SimTime(100),
    context: run.Context);
```

`Dispose()` у run scope сбрасывает sink-ы и финализирует `manifest.json` и `meta.txt`.

### 4) Используйте API сессии для долгоживущих циклов (вместо `RunTicks`)

```csharp
using var session = engine.CreateSession(
    state,
    seed: 12345,
    start: SimTime.Zero);

while (session.CurrentTime.Tick <= 10)
    session.TickOnce();

session.RunUntil(new SimTime(1000));
```

API сессии:
- `SimTime CurrentTime { get; }`
- `bool TickOnce(CancellationToken ct = default)`
- `void RunUntil(SimTime endInclusive, CancellationToken ct = default)`

## Конвенции артефактов

Все пути централизованы в `RunPaths` (`EpochSim.Execution.RunArtifacts`) и одинаковы для встраивания и CLI.

В `artifacts/<runId>/`:
- `events.jsonl` или `events.jsonl.gz`
- `trace.jsonl` или `trace.jsonl.gz`
- `profile.jsonl` или `profile.jsonl.gz`
- `statefp.jsonl`
- `manifest.json`
- `meta.txt`
- `snapshots/`
- `dumps/`
- `failure-report.json` и `failure-snapshot.json` при падении прогона

## CLI: быстрый старт

```bash
dotnet run --project src/EpochSim.Cli -- run artifacts
dotnet run --project src/EpochSim.Cli -- list-runs artifacts
dotnet run --project src/EpochSim.Cli -- inspect-run artifacts <runId>
```

CLI использует тот же builder/scope из `EpochSim.Hosting`, поэтому раскладка артефактов полностью совпадает со встраиванием.

## Чек-лист детерминизма

Избегайте недетерминированных источников в системах и обработчиках:
- `DateTime.Now` / `DateTime.UtcNow`
- `Guid.NewGuid()`
- произвольный `Random` (используйте `ctx.Rng`)
- зависимость от порядка обхода неупорядоченных коллекций
- скрытые гонки в многопоточности

## Сборка и тесты (.NET 10 + C# 14)

```bash
dotnet build EpochSim.slnx -m:1
dotnet test EpochSim.slnx -m:1
```

## Дополнительная документация

Русская:
- [Embedding.ru](https://github.com/Echways/EpochSim/blob/main/docs/Embedding.ru.md)
- [Artifacts.ru](https://github.com/Echways/EpochSim/blob/main/docs/Artifacts.ru.md)
- [Sessions.ru](https://github.com/Echways/EpochSim/blob/main/docs/Sessions.ru.md)
- [Determinism.ru](https://github.com/Echways/EpochSim/blob/main/docs/Determinism.ru.md)

English:
- [README.md](https://github.com/Echways/EpochSim/blob/main/README.md)
- [Embedding](https://github.com/Echways/EpochSim/blob/main/docs/Embedding.md)
- [Artifacts](https://github.com/Echways/EpochSim/blob/main/docs/Artifacts.md)
- [Sessions](https://github.com/Echways/EpochSim/blob/main/docs/Sessions.md)
- [Determinism](https://github.com/Echways/EpochSim/blob/main/docs/Determinism.md)
