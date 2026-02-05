# Артефакты

В EpochSim имена артефактов централизованы через `RunPaths`.

Каталог запуска:
- `artifacts/<runId>/`

Основные файлы:
- `events.jsonl` или `events.jsonl.gz`: журнал событий
- `trace.jsonl` или `trace.jsonl.gz`: трассировка
- `profile.jsonl` или `profile.jsonl.gz`: профилирование
- `statefp.jsonl`: отпечатки состояния
- `manifest.json`: манифест запуска
- `meta.txt`: компактные метаданные запуска

Каталоги:
- `snapshots/`: периодические снимки состояния
- `dumps/`: дампы нарушений инвариантов и fail-fast

Файлы при падении запуска:
- `failure-report.json`
- `failure-snapshot.json` (если включены snapshot-артефакты)

## Когда пишутся финальные файлы

`manifest.json` и `meta.txt` финализируются при `Dispose()` у `EpochSimRunScope<TState>`.
