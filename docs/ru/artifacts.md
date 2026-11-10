# Артефакты

`RunPaths` — единственный источник имён артефактов. Каждый прогон пишет в свой каталог:

```
artifacts/<runId>/
```

## Файлы

| Файл                                    | Содержимое                  |
| --------------------------------------- | --------------------------- |
| `events.jsonl`, `events.jsonl.gz`       | Лог событий                 |
| `trace.jsonl`, `trace.jsonl.gz`         | Поток трассировки           |
| `profile.jsonl`, `profile.jsonl.gz`     | Поток профилирования        |
| `statefp.jsonl`                         | Fingerprint'ы состояния     |
| `manifest.json`                         | Манифест прогона            |
| `meta.txt`                              | Компактные метаданные       |

Варианты `.gz` пишутся при включённом сжатии.

## Каталоги

| Каталог      | Содержимое                                          |
| ------------ | --------------------------------------------------- |
| `snapshots/` | Периодические снимки состояния                      |
| `dumps/`     | Дампы нарушений инвариантов и fail-fast             |

## Артефакты сбоя

Пишутся при падении прогона:

- `failure-report.json`
- `failure-snapshot.json`, если включён вывод снимков

## Финализация

`manifest.json` и `meta.txt` финализируются в `EpochSimRunScope<TState>.Dispose()`. Освободите run scope до чтения этих файлов.

## Чтение артефактов из кода

Артефакты лежат обычными файлами в каталоге прогона и читаются из кода. Лог событий необязателен и нужен для `verify-run`, `bisect` и `fast-replay`:

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
