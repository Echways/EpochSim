using EpochSim.Cli.App;
using EpochSim.Cli.Parsing;
using EpochSim.Fuzzing;

namespace EpochSim.Cli.Commands;

public sealed class FailuresCommand : ICliCommand
{
    public string Help =>
        """
        failures list [--store path]
        failures prune [--store path]
        failures export-test --framework xunit|nunit|mstest --configure Namespace.Type.Method --out dir
                             [--name suite] [--state-type Namespace.Type] [--store path]

        Work with the failures that Epoch.Fuzz recorded in the failure store (default: epochsim-failures.json).

          list         Print every stored failure.
          prune        Delete the failures already marked as fixed.
          export-test  Generate one regression test per stored failure. The tests call the static
                       method given by --configure, which must be 'static void Method(FuzzBuilder<TState>)'
                       and configure the same simulation as your Epoch.Fuzz call.

          --store path        Failure store file (default: epochsim-failures.json)
          --name suite        Suite to export when the store holds several (see FuzzBuilder.Name)
          --state-type type   State type of the suite, when the store does not record it
        """;

    public int Execute(CommandContext ctx, string[] args)
    {
        if (args.Length == 0 || CliParsing.IsOption(args[0]))
            throw new ArgumentException("Specify a subcommand: list, prune or export-test. Run 'failures --help' for usage.");

        var options = FailuresOptions.Parse(args[1..]);
        var store = new FuzzFailureStore(options.StorePath);

        return args[0].ToLowerInvariant() switch
        {
            "list" => List(store),
            "prune" => Prune(store),
            "export-test" => ExportTest(store, options),
            _ => throw new ArgumentException($"Unknown subcommand '{args[0]}'. Run 'failures --help' for usage.")
        };
    }

    private static int List(FuzzFailureStore store)
    {
        var failures = store.Load();

        if (failures.Count == 0)
        {
            Console.WriteLine($"No stored failures in '{store.Path}'.");
            return 0;
        }

        foreach (var failure in failures)
        {
            var otherSeeds = failure.OtherSeeds.Count > 0 ? $" (other seeds: {string.Join(", ", failure.OtherSeeds)})" : string.Empty;
            Console.WriteLine(
                $"{failure.Name}: seed={failure.Seed} {failure.Kind} {failure.Identity} tick={failure.Tick} " +
                $"{(failure.Fixed ? "fixed" : "open")}, first seen {failure.FirstSeen:yyyy-MM-dd}{otherSeeds}");
        }

        return 0;
    }

    private static int Prune(FuzzFailureStore store)
    {
        var pruned = 0;

        store.Update(failures =>
        {
            var remaining = failures.Where(failure => !failure.Fixed).ToArray();
            pruned = failures.Count - remaining.Length;
            return remaining;
        });

        Console.WriteLine($"Pruned {pruned} fixed failure{(pruned == 1 ? string.Empty : "s")} from '{store.Path}'.");
        return 0;
    }

    private static int ExportTest(FuzzFailureStore store, FailuresOptions options)
    {
        var framework = RegressionTestFramework.Parse(options.Framework);
        var configure = ConfigureMethod.Parse(options.Configure);
        var outputDirectory = options.OutputDirectory
                              ?? throw new ArgumentException("Option --out requires a directory.");

        var failures = SelectSuite(store, options.SuiteName);
        var stateType = options.StateType ?? failures[0].StateType;

        if (string.IsNullOrWhiteSpace(stateType))
            throw new ArgumentException("The store does not record the state type of this suite. Pass --state-type Namespace.Type.");

        Directory.CreateDirectory(outputDirectory);
        var path = Path.Combine(outputDirectory, RegressionTestGenerator.ClassName(configure) + ".cs");
        File.WriteAllText(path, RegressionTestGenerator.Generate(framework, configure, stateType, failures));

        Console.WriteLine($"Created: {path}");
        return 0;
    }

    private static IReadOnlyList<FuzzStoredFailure> SelectSuite(FuzzFailureStore store, string? suiteName)
    {
        var all = store.Load();

        if (all.Count == 0)
            throw new ArgumentException($"No stored failures in '{store.Path}'.");

        var suites = all.Select(failure => failure.Name).Distinct().Order(StringComparer.Ordinal).ToArray();

        if (suiteName is null && suites.Length > 1)
            throw new ArgumentException($"The store holds several suites ({string.Join(", ", suites)}). Pass --name.");

        var selected = all.Where(failure => failure.Name == (suiteName ?? suites[0])).ToArray();

        return selected.Length > 0
            ? selected
            : throw new ArgumentException($"No stored failures for suite '{suiteName}'. Known suites: {string.Join(", ", suites)}.");
    }
}
