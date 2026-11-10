<div align="center">

# EpochSim

**Find the seed that breaks your simulation.**
A deterministic tick engine for .NET 10: fuzz by seed, replay any run, bisect to the first tick that diverges.

**English** · [Русский](README.ru.md)

[![NuGet](https://img.shields.io/nuget/v/EpochSim.All)](https://www.nuget.org/packages/EpochSim.All)
[![CI](https://github.com/Echways/EpochSim/actions/workflows/ci.yml/badge.svg)](https://github.com/Echways/EpochSim/actions/workflows/ci.yml)
[![License](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

</div>

A simulation that cannot be reproduced cannot be debugged. EpochSim fixes the inputs of every tick: the same seed and the same commands always produce the same state, and a divergence is located to a single tick.

- **Seed fuzzing.** `Epoch.Fuzz` runs your code on thousands of seeds, names the failing seed and tick, and shows which state fields changed. Failures shrink to a minimal command scenario.
- **Deterministic by construction.** A run is defined by its state, seed and ordered input. Systems execute in a fixed order on one simulation thread.
- **Replay and bisect.** Verify a run against recorded fingerprints and find the first tick where an invariant breaks.
- **Test-framework agnostic.** `SimAssert` works in xUnit, NUnit and MSTest and turns found bugs into regression tests.
- **Roslyn analyzer.** Flags wall-clock time, `Random`, threads and other sources of non-determinism in simulation code.
- **CLI.** Run, inspect, verify, replay, bisect and fuzz from the terminal.

## Installation

```bash
dotnet add package EpochSim
dotnet add package EpochSim.Testing
dotnet tool install --global EpochSim.Cli
```

Requires .NET 10. `EpochSim.All` brings the whole module graph as a single dependency.

## Find a bug by seed

`Epoch.Fuzz` needs no rewrite of existing code. Call it from `Step`, state what must always hold in `Invariant`, pick the seeds.

<!-- snippet: throws FuzzFailedException -->
```csharp
using EpochSim;

var report = Epoch.Fuzz<Inventory>(f => f
    .State(() => new Inventory())
    .Step((inventory, ctx) => inventory.Tick(ctx.Rng.AsRandom()))
    .Invariant("StockNonNegative", inventory => inventory.Stock >= 0)
    .Seeds(1, 1000)
    .Ticks(500));

report.ThrowIfFailed();

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

The run finds the seeds on which the stock goes negative and names the tick and the state fields that changed:

```text
Fuzz failed: Failures=21, SeedsRun=1000, ElapsedMs=150

[InvariantViolation] seed=18 tick=19
  StockNonNegative: predicate returned false
  StateDiff:
    $.Stock: 1 -> -2
  Repro: .Seeds(18, 18).Ticks(19)

...
```

Add `.Seeds(18, 18).Ticks(19)` from the `Repro` line to run only that failure. More in [Fuzzing](docs/en/fuzzing.md).

## Run a simulation

```csharp
using EpochSim;

var state = new WorldState();
var engine = Epoch.CreateEngine<WorldState>();
engine.AddSystem("World", tick: ctx => ctx.State.Population++);

using var run = Epoch.QuickRun(state, rootDir: "artifacts");
run.RunTicks(engine, seed: 1, endTickInclusive: 100);

Console.WriteLine(state.Population);

public sealed class WorldState
{
    public int Population { get; set; }
}
```

Presets, external input, sessions and artifacts are covered in [Embedding](docs/en/embedding.md).

## Test it

```csharp
using EpochSim.Testing;

SimAssert.IsDeterministic<WorldState>(f => f
    .State(() => new WorldState())
    .Step((world, _) => world.Population++)
    .Seeds(1, 50)
    .Ticks(200));

public sealed class WorldState
{
    public int Population { get; set; }
}
```

A failure names the seed, the tick and the changed state fields. More in [Testing](docs/en/testing.md).

## Command line

```bash
epochsim fuzz --adapter fuzzbug --seeds 1..1000
epochsim verify-run artifacts <runId>
epochsim bisect artifacts <runId>
epochsim failures list
```

## Documentation

| Guide                                  | Contents                                            |
| -------------------------------------- | --------------------------------------------------- |
| [Embedding](docs/en/embedding.md)      | Presets, hosts, servers, external input             |
| [Fuzzing](docs/en/fuzzing.md)          | Seeds, shrinking, failure store, CLI, CI            |
| [Testing](docs/en/testing.md)          | `SimAssert`, golden fingerprints, regression tests  |
| [Determinism](docs/en/determinism.md)  | Rules and the analyzer ESIM001-ESIM006              |
| [Sessions](docs/en/sessions.md)        | Long-lived loops and stepping                       |
| [Artifacts](docs/en/artifacts.md)      | Run directory layout and file formats               |

Runnable examples are in [src/EpochSim.Samples](src/EpochSim.Samples).

## Development

```bash
dotnet build EpochSim.slnx -m:1
dotnet test EpochSim.slnx -m:1
```

## License

[MIT](LICENSE)
