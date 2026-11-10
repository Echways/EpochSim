# Testing

`EpochSim.Testing` adds simulation assertions to ordinary unit tests. It depends on no test framework: a failed check throws `SimulationAssertionException`, and every framework reports an unhandled exception as a failed test. Under the hood it runs [`Epoch.Fuzz`](fuzzing.md), so a failure message names the seed, the tick and the state fields that changed.

## Installation

```bash
dotnet add package EpochSim.Testing
```

It is a test dependency and is not part of `EpochSim.All`.

## Assertions

| Method                                              | Checks                                                                 |
| --------------------------------------------------- | ---------------------------------------------------------------------- |
| `SimAssert.IsDeterministic<TState>(configure)`      | Runs every seed twice and compares state after every tick. Always enables `CheckDeterminism`. |
| `SimAssert.InvariantsHold<TState>(configure)`       | Invariants, exceptions and, if `configure` enables it, determinism.    |
| `SimAssert.NoFailures(report)`                      | Throws when a `FuzzReport` has failures.                               |
| `SimAssert.MatchesGolden<TState>(configure, path, seed)` | Compares fingerprints of one seed with a stored file.             |
| `SimAssert.Regression<TState>(configure, seed, ticks, commandIndices)` | Replays one recorded failure. Fails while the bug is present. |
| `SimAssert.StoredFailuresStayFixed<TState>(configure, storePath)` | Replays every failure of the suite recorded in a failure store. |

`configure` is the same builder callback as in `Epoch.Fuzz`. A single seed is `Seeds(s, s)`.

## xUnit

```csharp
using EpochSim.Testing;
using Xunit;

public sealed class WorldTests
{
    [Fact]
    public void World_IsDeterministic()
    {
        SimAssert.IsDeterministic<WorldState>(f => f
            .State(() => new WorldState())
            .Engine(e => e.AddSystem("World", tick: ctx => ctx.State.Population++))
            .Seeds(1, 50)
            .Ticks(200));
    }
}
```

## NUnit

```csharp
using EpochSim.Testing;
using NUnit.Framework;

public sealed class WorldTests
{
    [Test]
    public void World_IsDeterministic()
    {
        SimAssert.IsDeterministic<WorldState>(f => f
            .State(() => new WorldState())
            .Engine(e => e.AddSystem("World", tick: ctx => ctx.State.Population++))
            .Seeds(1, 50)
            .Ticks(200));
    }
}
```

## MSTest

```csharp
using EpochSim.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class WorldTests
{
    [TestMethod]
    public void World_IsDeterministic()
    {
        SimAssert.IsDeterministic<WorldState>(f => f
            .State(() => new WorldState())
            .Engine(e => e.AddSystem("World", tick: ctx => ctx.State.Population++))
            .Seeds(1, 50)
            .Ticks(200));
    }
}
```

## Reading a failure

```
Fuzz failed: Failures=1, SeedsRun=50, ElapsedMs=12

[InvariantViolation] seed=4 tick=426
  QueueNonNegative: Pending=-1
  StateDiff:
    $.Completed: 198 -> 200
    $.Pending: 0 -> -1
  Repro: .Seeds(4, 4).Ticks(426)
```

The first line is the totals. Each block gives the failure kind, the seed, the tick, what broke, the state fields that changed on that tick, and the `Repro:` fragment that narrows the same test to the failing seed.

## Golden fingerprints

`MatchesGolden` is a regression test for "I changed behavior by accident":

```csharp
SimAssert.MatchesGolden<WorldState>(
    f => f
        .State(() => new WorldState())
        .Engine(e => e.AddSystem(new WorldSystem()))
        .Ticks(1000),
    goldenPath: "golden/world-seed-7.json",
    seed: 7);
```

The first run writes the file. It holds the state fingerprints of the seed on every 100th tick and on the last tick. Later runs compare against it and name the first tick that differs. After an intended change, run the tests once with `EPOCHSIM_UPDATE_GOLDEN=1` to rewrite the file and commit it.

## Regression tests for found bugs

A failure found by `Epoch.Fuzz` with `FailureStore()` is kept in `epochsim-failures.json` (see [Fuzzing](fuzzing.md#failure-store)). Two assertions turn it into a test.

`SimAssert.Regression` replays one seed. It is a thin wrapper over `Epoch.Fuzz`: it applies `configure`, then restricts the run to `Seeds(seed, seed)` and `Ticks(ticks)`, turns shrinking and the failure store off, and applies `OnlyCommands(commandIndices)` when the indices are given. The values to pass are in the `Repro:` line of the report, or generated for you by `epochsim failures export-test`:

```text
SimAssert.Regression<Inventory>(InventoryFuzz.Configure, 18, 19);
SimAssert.Regression<AccountState>(AccountFuzz.Configure, 1, 500, [4, 5]);
```

`SimAssert.StoredFailuresStayFixed` is one test that replays everything the store holds for the suite named by `configure` (`Name(...)`): the recorded scenario of every entry and the other seeds listed in it. It only reads the store and never marks anything as fixed, so it is safe in CI:

```text
SimAssert.StoredFailuresStayFixed<Inventory>(InventoryFuzz.Configure, "epochsim-failures.json");
```

A missing store file or a suite without entries passes. When a seed still fails, the exception carries the same report text as any other failed check.
