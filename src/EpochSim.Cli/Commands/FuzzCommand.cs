using EpochSim.Cli.App;
using EpochSim.Cli.Domain;
using EpochSim.Fuzzing;

namespace EpochSim.Cli.Commands;

public sealed class FuzzCommand : ICliCommand
{
    private const int FailuresFoundExitCode = 2;

    public string Help =>
        """
        fuzz [--adapter name] [--seeds A..B] [--ticks N] [--check-determinism] [--parallel N] [--out dir]

        Run the adapter's simulation over a range of seeds and report invariant violations,
        exceptions and non-determinism. Returns exit code 2 when a failure is found, 0 otherwise.

          --adapter name         Domain adapter (default: population, see list-adapters)
          --seeds A..B           Seed range, or a single seed N (default: 1..1000)
          --ticks N              Last tick of every run (default: 500)
          --check-determinism    Run every seed twice and compare state fingerprints
          --parallel N           Concurrent runs (default: processor count)
          --out dir              Artifacts directory for failed seeds (default: artifacts)
        """;

    public int Execute(CommandContext ctx, string[] args)
    {
        var options = FuzzOptions.Parse(args);
        var adapter = DomainAdapterRegistry.Resolve(options.AdapterName);
        return DomainAdapterDispatch.Invoke(adapter, new Invoker(options));
    }

    private static int Run<TState>(IDomainAdapter<TState> adapter, FuzzOptions options)
    {
        var report = Epoch.Fuzz<TState>(fuzz =>
        {
            fuzz.State(adapter.CreateInitialState)
                .Engine(adapter.ConfigureEngine)
                .Serializer(adapter.Serializer)
                .Invariants(adapter.CreateInvariants)
                .EventCodec(adapter.Codec)
                .Seeds(options.FromSeed, options.ToSeed)
                .Ticks(options.Ticks)
                .CheckDeterminism(options.CheckDeterminism)
                .ArtifactsRoot(options.OutputDirectory);

            if (options.Parallelism is { } degree)
                fuzz.Parallelism(degree);
        });

        Console.WriteLine($"SeedsRun={report.SeedsRun} Failures={report.Failures.Count} ElapsedMs={(long)report.Duration.TotalMilliseconds}");

        foreach (var failure in report.Failures)
        {
            var cliFailure = failure with
            {
                ReproCode = options.ReproCommand(
                    adapter.Name,
                    failure.Seed,
                    failure.Tick,
                    failure.Kind == FuzzFailureKind.NonDeterminism)
            };

            Console.WriteLine();
            Console.WriteLine(cliFailure.ToText());
        }

        return report.Passed ? 0 : FailuresFoundExitCode;
    }

    private sealed class Invoker(FuzzOptions options) : IDomainAdapterInvoker
    {
        public int Invoke<TState>(IDomainAdapter<TState> adapter)
            => Run(adapter, options);
    }
}
