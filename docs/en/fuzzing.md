# Fuzzing

`Epoch.Fuzz` runs your simulation over a range of seeds and tells you which seed breaks it, on which tick, and which state fields changed on that tick. It finds three kinds of failure:

- `InvariantViolation`: an `IInvariant<TState>` returned `false`.
- `Exception`: a system or command handler threw.
- `NonDeterminism`: two runs of the same seed produced different state (needs `CheckDeterminism`).

Every failure is reproducible with the same seed.

## Minimal example

```csharp
using EpochSim;

var report = Epoch.Fuzz<JobQueueState>(f => f
    .State(() => new JobQueueState())
    .Engine(e => e.AddSystem(new JobQueueSystem()))
    .Invariants(() => [new QueueNonNegativeInvariant()])
    .Seeds(1, 1000)
    .Ticks(500));

Console.WriteLine(report.ToText());
```

Output:

```
Fuzz failed: Failures=14, SeedsRun=1000, ElapsedMs=81

[InvariantViolation] seed=4 tick=426
  QueueNonNegative: Pending=-1
  StateDiff:
    $.Completed: 198 -> 200
    $.Pending: 0 -> -1
  Repro: .Seeds(4, 4).Ticks(426)
```

In a test, call `report.ThrowIfFailed()` or use [EpochSim.Testing](testing.md).

The runnable sample is in [src/EpochSim.Samples/Fuzzing](../../src/EpochSim.Samples/Fuzzing/): a job queue whose counter goes negative on a rare, seed-dependent sequence of draws from `ctx.Rng`.

## Builder

| Method                                    | Required | Meaning                                                                            |
| ----------------------------------------- | -------- | ---------------------------------------------------------------------------------- |
| `State(Func<TState>)`                     | yes      | Factory for a fresh initial state. Called for every run.                           |
| `Engine(Action<SimulationEngine<TState>>)` | yes     | Registers systems and command handlers on a new engine. Called for every run.      |
| `Ticks(long)`                             | yes      | Last tick of every run, inclusive.                                                 |
| `Seeds(ulong from, ulong to)`             | yes      | Inclusive seed range.                                                              |
| `Seeds(IEnumerable<ulong>)`               | yes      | Explicit seeds. Use either overload.                                               |
| `Serializer(IStateSerializer<TState>)`    | no       | Used for fingerprints and diffs. Default: `JsonStateSerializer<TState>`.           |
| `Invariants(Func<IEnumerable<IInvariant<TState>>>)` | no | Checked after every tick. A factory, so every run gets its own instances.   |
| `Commands(Action<FuzzCommandPlan>)`       | no       | Generates input commands for a seed. See below.                                    |
| `CheckDeterminism(bool)`                  | no       | Run every seed twice and compare fingerprints after every tick. Default: `false`.  |
| `Parallelism(int)`                        | no       | Concurrent runs. Default: `Environment.ProcessorCount`.                            |
| `StopAtFirstFailure(bool)`                | no       | Stop handing out new seeds after the first failure. Default: `false`.              |
| `ArtifactsRoot(string)`                   | no       | Write full run artifacts for failed seeds (at most the first 10, by seed).         |
| `EventCodec(IEventCodecV2)`               | no       | Adds the event log to those artifacts.                                             |
| `Progress(IProgress<FuzzProgress>)`       | no       | Called after every finished seed.                                                  |

Invalid values throw immediately: a missing required parameter, `Seeds` with `from > to`, `Ticks < 0`, `Parallelism < 1`.

## Generating commands

`Commands` is called once per seed, before the run starts. `plan.Rng` is a deterministic stream derived from the seed. It is separate from `ctx.Rng`, so generating commands never shifts the randomness of your systems.

```csharp
.Commands(plan =>
{
    for (var i = 0; i < 20; i++)
        plan.Enqueue(plan.Rng.NextInt(0, (int)plan.EndTick), new AddPopulation(plan.Rng.NextInt(1, 10)));
})
```

`Enqueue(tick, command)` accepts ticks from `0` to `plan.EndTick`. Commands are delivered through a `ScheduledCommandInbox`.

## Reading the report

`FuzzReport` has `Passed`, `SeedsRun`, `Duration`, `Failures`, `ToText()` and `ThrowIfFailed()`. `Failures` is always sorted by seed, whatever the `Parallelism`.

Every `FuzzFailure` carries the `Seed`, the `Kind`, the `Tick`, a `Summary`, the `StateDiff` for that tick, the `ArtifactsDir` (when `ArtifactsRoot` is set) and `ReproCode`.

`StateDiff` lists the fields that changed on the failing tick: from the end of the previous tick to the failing state. For `NonDeterminism` it lists the fields in which the two runs differ. Paths look like `$.Population`, `$.Items[3].Count`, `$["a.b"]`.

## Reproducing a failure

Add the `ReproCode` to your `Epoch.Fuzz` call to narrow it to the failing seed:

```csharp
.Seeds(4, 4).Ticks(426)
```

The run fails on the same tick. With `ArtifactsRoot` the directory `fuzz-<seed>` also holds `manifest.json`, `statefp.jsonl`, the failure report and, with an `EventCodec`, the event log, so `epochsim verify-run`, `bisect` and `repro` can work on it.

## Checking determinism

`CheckDeterminism(true)` runs each seed twice and compares the state fingerprint after every tick. It catches `static` state, `DateTime.UtcNow`, `Random`, hash-order dependence and anything else that makes two runs of one seed differ. Runs share no mutable state except what your code shares, such as `static` fields: that is what this check finds.

## Parallelism

Seeds run in parallel, one run per thread. The report does not depend on `Parallelism`. `StopAtFirstFailure(true)` stops handing out new seeds, but runs already in flight finish and their failures are reported. Which seeds were in flight depends on timing, so the set of reported failures is stable only with `Parallelism(1)`.

## Command line

```bash
epochsim fuzz --adapter fuzzbug --seeds 1..1000 --ticks 500
```

| Option                | Default      | Meaning                                         |
| --------------------- | ------------ | ----------------------------------------------- |
| `--adapter name`      | `population` | Domain adapter, see `epochsim list-adapters`    |
| `--seeds A..B`        | `1..1000`    | Seed range, or a single seed `N`                |
| `--ticks N`           | `500`        | Last tick of every run                          |
| `--check-determinism` | off          | Run every seed twice                            |
| `--parallel N`        | CPU count    | Concurrent runs                                 |
| `--out dir`           | `artifacts`  | Artifacts directory for failed seeds            |

Exit code `0`: no failures. Exit code `2`: failures found, or invalid arguments. The CLI works with adapters registered in `DomainAdapterRegistry`; in your own code the library API is the main entry point.

## Running in CI

```csharp
[Fact]
public void World_SurvivesFuzzing()
{
    Epoch.Fuzz<WorldState>(f => f
        .State(() => new WorldState())
        .Engine(e => e.AddSystem(new WorldSystem()))
        .Invariants(() => [new PopulationNonNegativeInvariant()])
        .Seeds(1, 2000)
        .Ticks(500)
        .ArtifactsRoot("fuzz-artifacts")
        .CheckDeterminism()).ThrowIfFailed();
}
```

Upload `fuzz-artifacts` as a build artifact when the test fails.
