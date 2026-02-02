# EpochSim

Deterministic, event-driven simulation engine for C# with replay, snapshots, and observability.

## Features
- Deterministic RNG and tick-based scheduling
- Command/event pipeline with event-log replay
- Snapshotting for fast replay
- Tracing, profiling, and state fingerprinting (JSONL)
- CLI for running, validating, and inspecting simulations

## Quick start
Run the sample simulation and write artifacts to `artifacts/`:

```bash
dotnet run --project src/EpochSim.Cli run artifacts
```

Validate a run with invariants and dumps:

```bash
dotnet run --project src/EpochSim.Cli validate-run artifacts
```

Run tests:

```bash
dotnet test tests/EpochSim.Determinism.Tests/EpochSim.Determinism.Tests.csproj
```

## CLI commands
- `run`
- `validate-run`
- `fast-replay`
- `verify-run`
- `event-stats`
- `timeline`
- `pretty-inspect`
- `list-runs`
- `inspect-run`
- `bisect`
- `repro`

## Repository layout
- `src/EpochSim.Kernel` - core types, scheduling, determinism, messaging
- `src/EpochSim.Execution` - simulation engine and middleware
- `src/EpochSim.Serialization` - event logs, snapshots, state serialization
- `src/EpochSim.Observability` - tracing and profiling sinks
- `src/EpochSim.Samples` - sample domain model (Population)
- `src/EpochSim.Cli` - CLI runner and tools
- `tests/EpochSim.Determinism.Tests` - determinism and replay tests
