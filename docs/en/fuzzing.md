# Fuzzing

`Epoch.Fuzz` runs your code over a range of seeds and tells you which seed breaks it, on which tick, and which state fields changed on that tick. It finds three kinds of failure:

- `InvariantViolation`: an invariant returned `false`.
- `Exception`: a system, step or command handler threw.
- `NonDeterminism`: two runs of the same seed produced different state (needs `CheckDeterminism`).

Every failure is reproducible with the same seed. When commands drive the simulation, the report also reduces them to a minimal scenario ([Shrinking](#shrinking)), and failing seeds can be kept in a file so that a bug found once is checked forever ([Failure store](#failure-store)).

## Minimal example

`Inventory` is an ordinary class that knows nothing about EpochSim. `Step` calls it on every tick, `Invariant` says what must always hold, `Seeds` and `Ticks` say how much to try:

```csharp
using EpochSim;

var report = Epoch.Fuzz<Inventory>(f => f
    .State(() => new Inventory())
    .Step((inventory, ctx) => inventory.Tick(ctx.Rng.AsRandom()))
    .Invariant("StockNonNegative", inventory => inventory.Stock >= 0)
    .Seeds(1, 1000)
    .Ticks(500));

Console.WriteLine(report.ToText());

public sealed class Inventory
{
    public int Stock { get; private set; } = 5;

    public void Tick(Random random)
    {
        if (random.Next(0, 3) == 0)
            Stock += 4;

        var demand = random.Next(0, 3);
        if (Stock >= demand)
            Stock -= demand;

        if (random.Next(0, 400) == 0)
            Stock -= 5;
    }
}
```

Output (the first of 21 failures; `ElapsedMs` depends on the machine):

```text
Fuzz failed: Failures=21, SeedsRun=1000, ElapsedMs=150

[InvariantViolation] seed=18 tick=19
  StockNonNegative: predicate returned false
  StateDiff:
    $.Stock: 1 -> -2
  Repro: .Seeds(18, 18).Ticks(19)
```

In a test, call `report.ThrowIfFailed()` or use [EpochSim.Testing](testing.md). The same class lives in [src/EpochSim.Samples/Onboarding](../../src/EpochSim.Samples/Onboarding/), where the test suite runs the call above against it.

## Connecting existing code

You do not have to restructure your code into systems, invariant classes and serializable state.

| Method                                  | What it does                                                                                           |
| --------------------------------------- | ------------------------------------------------------------------------------------------------------ |
| `Step(Action<TState, StepContext>)`     | Runs on every tick, after the systems added with `Engine`. Several steps run in registration order.   |
| `Invariant(string, Func<TState, bool>)` | A named invariant, checked after every tick. The name goes into the report and into `Signature`.       |
| `ctx.Rng.AsRandom()`                    | A `System.Random` over the deterministic stream, for code that already takes a `Random`.               |
| `ctx.TimeProvider`                      | A `TimeProvider` (a `FakeTimeProvider`) that starts at `StepContext.ClockStart` and advances `StepContext.TickDuration` (one second) per tick. |
| `ctx.Tick`                              | The current tick.                                                                                      |
| `Project(Func<TState, object>)`         | The part of the state used for fingerprints and `StateDiff`, when the state itself cannot be serialized to JSON. |

`AsRandom()` returns one adapter per stream, so calling it on every tick costs a table lookup. Values come from the same stream as `ctx.Rng`: they are the same for the same seed and never read the system entropy.

Code that reads time takes a `TimeProvider` instead of calling `DateTime.UtcNow`, and the run stays deterministic:

```csharp
using EpochSim;

var report = Epoch.Fuzz<Cache>(f => f
    .State(() => new Cache(TimeSpan.FromSeconds(5)))
    .Step((cache, ctx) =>
    {
        var random = ctx.Rng.AsRandom();
        var now = ctx.TimeProvider.GetUtcNow();
        cache.Get(random.Next(0, 20), now);
        cache.Put(random.Next(0, 20), now);
        if (cache.Count > 15)
            cache.Evict(now);
    })
    .Invariant("NeverServesStaleEntries", cache => cache.StaleHits == 0)
    .CheckDeterminism()
    .Seeds(1, 100)
    .Ticks(200));

Console.WriteLine(report.Failures.Count > 0 ? report.Failures[0].ToText() : report.ToText());

public sealed class Cache(TimeSpan lifetime)
{
    private readonly Dictionary<int, DateTimeOffset> _storedAt = [];

    public int Count => _storedAt.Count;
    public int StaleHits { get; private set; }

    public void Put(int key, DateTimeOffset now) => _storedAt[key] = now;

    public void Get(int key, DateTimeOffset now)
    {
        if (_storedAt.TryGetValue(key, out var storedAt) && now - storedAt > lifetime)
            StaleHits++;
    }

    public void Evict(DateTimeOffset now)
    {
        foreach (var key in _storedAt.Where(entry => now - entry.Value > lifetime).Select(entry => entry.Key).ToList())
            _storedAt.Remove(key);
    }
}
```

State that cannot be serialized to JSON (cycles, dictionaries keyed by objects) makes the default serializer throw. `Project` replaces it with a projection of the fields you care about:

```csharp
using EpochSim;

var report = Epoch.Fuzz<Graph>(f => f
    .State(() => new Graph())
    .Step((graph, ctx) => graph.Grow(ctx.Rng.AsRandom()))
    .Project(graph => new { graph.NodeCount, graph.MaxChildren })
    .Invariant("AtMostThreeChildren", graph => graph.MaxChildren <= 3)
    .CheckDeterminism()
    .Seeds(1, 5)
    .Ticks(100));

Console.WriteLine(report.Failures[0].ToText());

public sealed class Graph
{
    private readonly List<Node> _nodes = [];

    public int NodeCount => _nodes.Count;
    public int MaxChildren => _nodes.Count == 0 ? 0 : _nodes.Max(node => node.Children.Count);
    public IReadOnlyList<Node> Nodes => _nodes;

    public void Grow(Random random)
    {
        var parent = _nodes.Count == 0 ? null : _nodes[random.Next(_nodes.Count)];
        var node = new Node(parent);
        parent?.Children.Add(node);
        _nodes.Add(node);
    }
}

public sealed class Node(Node? parent)
{
    public Node? Parent { get; } = parent;
    public List<Node> Children { get; } = [];
}
```

```text
[InvariantViolation] seed=1 tick=6
  AtMostThreeChildren: predicate returned false
  StateDiff:
    $.MaxChildren: 3 -> 4
    $.NodeCount: 6 -> 7
  Repro: .Seeds(1, 1).Ticks(6)
```

`Project` cannot be combined with `Serializer`; both decide how the state is serialized. A projection is one way: it is used for fingerprints and diffs, not to restore state.

The determinism analyzer (rules ESIM001 to ESIM006) treats the lambdas passed to `Step` and `Invariant` as simulation code, like `AddSystem`: `new Random()`, `DateTime.Now` and the like are reported there.

## Builder

| Method                                    | Required | Meaning                                                                            |
| ----------------------------------------- | -------- | ---------------------------------------------------------------------------------- |
| `State(Func<TState>)`                     | yes      | Factory for a fresh initial state. Called for every run.                           |
| `Engine(Action<SimulationEngine<TState>>)` | one of `Engine` or `Step` | Registers systems and command handlers on a new engine. Called for every run. |
| `Step(Action<TState, StepContext>)`       | one of `Engine` or `Step` | Per-tick logic without writing a system. See above.                           |
| `Ticks(long)`                             | yes      | Last tick of every run, inclusive.                                                 |
| `Seeds(ulong from, ulong to)`             | yes      | Inclusive seed range.                                                              |
| `Seeds(IEnumerable<ulong>)`               | yes      | Explicit seeds. Use either overload.                                               |
| `Invariant(string, Func<TState, bool>)`   | no       | A named invariant. Can be repeated.                                                |
| `Invariants(Func<IEnumerable<IInvariant<TState>>>)` | no | Invariant classes. A factory, so every run gets its own instances. Checked before the `Invariant` predicates. |
| `Serializer(IStateSerializer<TState>)`    | no       | Used for fingerprints and diffs. Default: `JsonStateSerializer<TState>`.           |
| `Project(Func<TState, object>)`           | no       | Alternative to `Serializer`: serialize a projection of the state.                  |
| `Commands(Action<FuzzCommandPlan>)`       | no       | Generates input commands for a seed. See below.                                    |
| `Shrink(bool)`                            | no       | Minimize the commands of a failed seed. Default: `true` when `Commands` is set.    |
| `OnlyCommands(IReadOnlyList<int>)`        | no       | Keep only the commands with these indices of the generated plan.                   |
| `CheckDeterminism(bool)`                  | no       | Run every seed twice and compare fingerprints after every tick. Default: `false`.  |
| `Name(string)`                            | no       | Stable name of this suite in the failure store. Default: the full name of `TState`. |
| `FailureStore(string? path = null)`       | no       | Keep failing seeds in a file and replay them first. Default path: `epochsim-failures.json`. |
| `Parallelism(int)`                        | no       | Concurrent runs. Default: `Environment.ProcessorCount`.                            |
| `StopAtFirstFailure(bool)`                | no       | Stop handing out new seeds after the first failure. Default: `false`.              |
| `ArtifactsRoot(string)`                   | no       | Write full run artifacts for failed seeds (at most the first 10, by seed).         |
| `EventCodec(IEventCodecV2)`               | no       | Adds the event log to those artifacts.                                             |
| `Progress(IProgress<FuzzProgress>)`       | no       | Called after every finished seed.                                                  |

Invalid values throw immediately: a missing required parameter, `Seeds` with `from > to`, `Ticks < 0`, `Parallelism < 1`, an empty `Name`, a negative index in `OnlyCommands`.

## Generating commands

`Commands` is called once per seed, before the run starts. `plan.Rng` is a deterministic stream derived from the seed. It is separate from `ctx.Rng`, so generating commands never shifts the randomness of your systems.

```csharp
using EpochSim;
using EpochSim.Kernel.Messaging;

var report = Epoch.Fuzz<Counter>(f => f
    .State(() => new Counter())
    .Engine(e => e.OnCommand<Add>((counter, command, _) => counter.Total += command.Amount))
    .Commands(plan =>
    {
        for (var i = 0; i < 20; i++)
            plan.Enqueue(plan.Rng.NextInt(0, (int)plan.EndTick + 1), new Add(plan.Rng.NextInt(1, 10)));
    })
    .Invariant("TotalBelowLimit", counter => counter.Total < 100)
    .Seeds(1, 10)
    .Ticks(50));

Console.WriteLine(report.Passed);

public sealed class Counter
{
    public int Total { get; set; }
}

public sealed record Add(int Amount) : ICommand;
```

`Enqueue(tick, command)` accepts ticks from `0` to `plan.EndTick`. Commands are delivered through a `ScheduledCommandInbox`. The generator must be deterministic: it may use only `plan.Rng` and `plan.Seed`. The index of a command is its position in the order of `Enqueue` calls.

## Reading the report

`FuzzReport` has `Passed`, `SeedsRun`, `Duration`, `Failures`, `NewFailures`, `KnownFailures`, `FixedSeeds`, `ToText()` and `ThrowIfFailed()`. `Failures` is always sorted by seed, whatever the `Parallelism`.

Every `FuzzFailure` carries the `Seed`, the `Kind`, the `Tick`, a `Summary`, the `StateDiff` for that tick, the `ArtifactsDir` (when `ArtifactsRoot` is set) and `ReproCode`. It also has the `Identity` (the invariant name, or the exception type) and the `Signature`, a 16-character hash of the kind and the identity that is the same for every seed that hits the same bug.

`StateDiff` lists the fields that changed on the failing tick: from the end of the previous tick to the failing state. For `NonDeterminism` it lists the fields in which the two runs differ. Paths look like `$.Population`, `$.Items[3].Count`, `$["a.b"]`.

## Reproducing a failure

Add the `ReproCode` to your `Epoch.Fuzz` call to narrow it to the failing seed:

```text
.Seeds(18, 18).Ticks(19)
```

The run fails on the same tick. With `ArtifactsRoot` the directory `fuzz-<seed>` also holds `manifest.json`, `statefp.jsonl`, the failure report and, with an `EventCodec`, the event log, so `epochsim verify-run`, `bisect` and `repro` can work on it.

When `Commands` is configured, `ReproCode` keeps the configured `Ticks`, not the failing tick: the command plan is generated for the configured length, and a different length may generate a different plan. It also carries `OnlyCommands` for a shrunk failure.

## Shrinking

A seed with `Commands` can carry hundreds of commands, and a failure rarely needs all of them. When a seed fails, EpochSim reduces its command plan to a minimal set that still fails, using delta debugging (ddmin). No maintained .NET library does this, so the algorithm is part of EpochSim.

The `Account` sample of [src/EpochSim.Samples/Fuzzing](../../src/EpochSim.Samples/Fuzzing/) generates 300 deposits and withdrawals per seed and forgets to check the balance on withdrawal:

```csharp
using EpochSim;
using EpochSim.Samples.Fuzzing;

var report = Epoch.Fuzz<AccountState>(f => f
    .State(() => new AccountState())
    .Engine(AccountLedger.Register)
    .Invariants(() => [new BalanceNonNegativeInvariant()])
    .Commands(AccountLedger.PlanCommands)
    .Seeds(1, 3)
    .Ticks(500));

Console.WriteLine(report.Failures[0].ToText());
```

```text
[InvariantViolation] seed=1 tick=489
  BalanceNonNegative: Balance=-7
  Commands: 300 -> 2 (shrunk in 13 attempts)
    #4 tick 489: WithdrawCommand
    #5 tick 157: WithdrawCommand
  StateDiff:
    $.Balance: 44 -> -7
  Repro: .Seeds(1, 1).Ticks(500).OnlyCommands([4, 5])
```

Two of 300 commands are enough. The `StateDiff` belongs to the minimal scenario, and `ReproCode` replays exactly it: `OnlyCommands` keeps the commands with these indices of the generated plan, so the repro is code and needs no serialization of commands.

How it works:

- A candidate fails the same way when its `Kind` and its identity (the invariant name or the exception type) are the same. The failing tick may move, and that is fine.
- The search is deterministic: candidates are tried in a fixed order, with no randomness, and one failure is shrunk on one thread. The result does not depend on `Parallelism`.
- The budget is 1000 attempts or 10 seconds per failure. When it runs out, the best scenario found so far is returned; it still fails, and the report says that the budget was exhausted and the result may not be minimal.
- With `OnlyCommands` set by you, the search starts from your selection.

What is not shrunk:

- Without `Commands` there is nothing to shrink. The seed and the tick are already minimal, because a run stops at the first failure. `OriginalCommandCount` is `0` and `MinimalCommandIndices` is `null`.
- A `NonDeterminism` failure is not shrunk, because a smaller scenario cannot be checked against replays that disagree. The report says so (`ShrinkSkipReason`).
- Commands are removed, not moved to other ticks, and `Ticks` is not reduced: a repro must be expressible by `OnlyCommands`, and the plan depends on `Ticks`.

`Shrink(false)` turns shrinking off. In the CLI, use `--no-shrink`.

## Failure store

A bug found once should be checked always: on your machine, in CI and on your colleagues' machines. `FailureStore()` keeps failing seeds in `epochsim-failures.json`, a file meant to be committed to git, like `.hypothesis/`:

```csharp
using EpochSim;

var report = Epoch.Fuzz<Inventory>(f => f
    .Name("inventory")
    .State(() => new Inventory())
    .Step((inventory, ctx) => inventory.Tick(ctx.Rng.AsRandom()))
    .Invariant("StockNonNegative", inventory => inventory.Stock >= 0)
    .Seeds(1, 1000)
    .Ticks(500)
    .FailureStore());

Console.WriteLine(report.ToText());

public sealed class Inventory
{
    public int Stock { get; private set; } = 5;

    public void Tick(Random random)
    {
        if (random.Next(0, 3) == 0)
            Stock += 4;

        var demand = random.Next(0, 3);
        if (Stock >= demand)
            Stock -= demand;

        if (random.Next(0, 400) == 0)
            Stock -= 5;
    }
}
```

The first run writes the file. All 21 failing seeds hit the same bug, so they make one entry: the lowest seed with its tick, the other 20 seeds in `otherSeeds`, and a `signature`. The second run prints the same failures marked as known:

```text
Fuzz failed: Failures=21 (new=0, known=21), SeedsRun=1000, ElapsedMs=145

Known failures:

[InvariantViolation] seed=18 tick=19 (known)
  StockNonNegative: predicate returned false
  StateDiff:
    $.Stock: 1 -> -2
  Repro: .Seeds(18, 18).Ticks(19)
```

How the store behaves:

- `Name` identifies the suite in the file. The default is the full name of `TState`. Suites with different names do not touch each other's entries.
- The seeds stored for the suite run first, whatever `Seeds` says. A seed that is also in the range runs once.
- Entries are deduplicated by `Signature`: the kind plus the invariant name or exception type. A new seed with a known signature joins `otherSeeds`.
- The report separates new failures from known ones. `ThrowIfFailed` throws in both cases: a known bug is still a bug.
- When every seed of an entry passes, the entry is marked `fixed` and the report says `Previously failing seed 18 now passes.` once. Entries are never deleted automatically. A fixed entry keeps being replayed; if its bug comes back, it is reopened and reported as a new failure.
- A seed that was not run, for instance because of `StopAtFirstFailure`, does not count as passed.
- Writes are atomic (a temporary file, then a move) and serialized between threads and processes, so parallel test runs do not corrupt the file. The file is sorted, so the diffs in git stay small.
- The file has a `version`. A file written by a newer EpochSim is rejected with a message instead of being misread. A file that is not valid JSON is rejected and never overwritten.
- Without `FailureStore`, nothing is read or written.

The `failures` command works on the file:

```bash
epochsim failures list
epochsim failures prune
epochsim failures export-test --framework xunit --configure MyApp.InventoryFuzz.Configure --out tests/
```

- `list` prints every entry, `prune` deletes the fixed ones.
- `export-test` writes one regression test per entry, for xunit, nunit or mstest. A configuration of a simulation contains lambdas and cannot be generated, so you point `--configure` at a `public static void Configure(FuzzBuilder<TState>)` method that builds the same simulation as your `Epoch.Fuzz` call, and the tests call it. If the store holds several suites, pass `--name`. The state type is read from the store; `--state-type` overrides it.

For the store above, the generated `InventoryFuzzRegressionTests.cs` is:

```text
using EpochSim.Testing;
using Xunit;

namespace MyApp;

public sealed class InventoryFuzzRegressionTests
{
    [Fact]
    public void Seed18_StockNonNegative_AtTick19()
    {
        SimAssert.Regression<global::EpochSim.Samples.Onboarding.Inventory>(global::MyApp.InventoryFuzz.Configure, 18, 19);
    }
}
```

The test fails until the bug is fixed and keeps guarding the fix afterwards. `SimAssert.Regression` and `SimAssert.StoredFailuresStayFixed` are described in [Testing](testing.md).

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
| `--no-shrink`         | off          | Keep the full command plan of a failed seed     |
| `--store path`        | none         | Record failures in a failure store, named after the adapter |

Exit code `0`: no failures. Exit code `2`: failures found, or invalid arguments. The CLI works with adapters registered in `DomainAdapterRegistry`; in your own code the library API is the main entry point. `epochsim init` writes a starter project that already contains an `Epoch.Fuzz` call.

## Running in CI

Call `report.ThrowIfFailed()` from a test, or use `SimAssert` from [Testing](testing.md), with a large `Seeds` range and `.ArtifactsRoot("fuzz-artifacts")`. Upload `fuzz-artifacts` as a build artifact when the test fails. Commit `epochsim-failures.json`, so CI replays the seeds that failed on your machine, and the other way round.
