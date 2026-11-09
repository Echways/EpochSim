# Artifacts

`RunPaths` is the single source of artifact naming. Every run writes to its own directory:

```
artifacts/<runId>/
```

## Files

| File                                    | Content                    |
| --------------------------------------- | -------------------------- |
| `events.jsonl`, `events.jsonl.gz`       | Event log                  |
| `trace.jsonl`, `trace.jsonl.gz`         | Trace stream               |
| `profile.jsonl`, `profile.jsonl.gz`     | Profiling stream           |
| `statefp.jsonl`                         | State fingerprints         |
| `manifest.json`                         | Run manifest               |
| `meta.txt`                              | Compact run metadata       |

The `.gz` variants are written when compression is enabled.

## Directories

| Directory    | Content                                      |
| ------------ | -------------------------------------------- |
| `snapshots/` | Periodic state snapshots                     |
| `dumps/`     | Invariant violation and fail-fast dumps      |

## Failure artifacts

Written when a run fails:

- `failure-report.json`
- `failure-snapshot.json`, when snapshot output is enabled

## Finalization

`manifest.json` and `meta.txt` are finalized in `EpochSimRunScope<TState>.Dispose()`. Dispose the run scope before reading them.
