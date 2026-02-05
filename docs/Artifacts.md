# Artifacts

EpochSim uses `RunPaths` as the single source of naming for artifacts.

Run directory:
- `artifacts/<runId>/`

Core files:
- `events.jsonl` or `events.jsonl.gz`: event log
- `trace.jsonl` or `trace.jsonl.gz`: trace stream
- `profile.jsonl` or `profile.jsonl.gz`: profiling stream
- `statefp.jsonl`: state fingerprints
- `manifest.json`: run manifest
- `meta.txt`: compact run metadata

Directories:
- `snapshots/`: periodic state snapshots
- `dumps/`: violation and fail-fast dumps

Failure artifacts (when run fails):
- `failure-report.json`
- `failure-snapshot.json` (if snapshot output enabled)

## Note about finalization

`manifest.json` and `meta.txt` are finalized on `EpochSimRunScope<TState>.Dispose()`.
