using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EpochSim.Cli.Commands;
using EpochSim.Cli.Domain;

namespace EpochSim.Cli.App;

public sealed class CliApp
{
    private readonly Dictionary<string, ICliCommand> _commands;

    public CliApp()
    {
        _commands = new Dictionary<string, ICliCommand>(StringComparer.OrdinalIgnoreCase)
        {
            ["init"] = new InitCommand(),
            ["run"] = new RunCommand(),
            ["validate-run"] = new ValidateRunCommand(),
            ["fast-replay"] = new FastReplayCommand(),
            ["verify-run"] = new VerifyRunCommand(),
            ["event-stats"] = new EventStatsCommand(),
            ["timeline"] = new TimelineCommand(),
            ["pretty-inspect"] = new PrettyInspectCommand(),
            ["list-runs"] = new ListRunsCommand(),
            ["inspect-run"] = new InspectRunCommand(),
            ["bisect"] = new BisectCommand(),
            ["repro"] = new ReproCommand(),
            ["list-adapters"] = new ListAdaptersCommand()
        };
    }

    public Task<int> RunAsync(string[] args)
    {
        var commandName = args.Length > 0 ? args[0] : "run";
        var root = args.Length > 1 ? args[1] : "artifacts";
        var commandArgs = args.Length > 2 ? args.Skip(2).ToArray() : Array.Empty<string>();

        if (string.Equals(commandName, "init", StringComparison.OrdinalIgnoreCase))
        {
            root = Directory.GetCurrentDirectory();
            commandArgs = args.Length > 1 ? args.Skip(1).ToArray() : Array.Empty<string>();
        }

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        var ctx = new CommandContext(
            root,
            DomainAdapterRegistry.Default,
            cts.Token);

        if (!_commands.TryGetValue(commandName, out var command))
        {
            PrintUsage();
            return Task.FromResult(2);
        }

        if (commandArgs.Any(a => string.Equals(a, "--help", StringComparison.OrdinalIgnoreCase)
                              || string.Equals(a, "-h", StringComparison.OrdinalIgnoreCase)))
        {
            Console.WriteLine(command.Help);
            return Task.FromResult(0);
        }

        try
        {
            var rc = command.Execute(ctx, commandArgs);
            return Task.FromResult(rc);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return Task.FromResult(2);
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  init [targetDir]");
        Console.WriteLine("  run <artifactsRoot> [runId] [--end-tick N] [--seed N] [--guard-state] [--compress]");
        Console.WriteLine("      [--fingerprint-every N] [--snapshot-every N] [--max-pump-steps N]");
        Console.WriteLine("      [--max-event-dispatches-per-tick N] [--rng-version v1|v2] [--cancel-after-ms N]");
        Console.WriteLine("  validate-run <artifactsRoot> [runId] [--end-tick N] [--seed N] [--guard-state] [--compress]");
        Console.WriteLine("      [--fingerprint-every N] [--snapshot-every N] [--max-pump-steps N]");
        Console.WriteLine("      [--max-event-dispatches-per-tick N] [--rng-version v1|v2] [--cancel-after-ms N]");
        Console.WriteLine("  fast-replay <artifactsRoot> [runId] [--end-tick N] [--seed N]");
        Console.WriteLine("  verify-run <artifactsRoot> [runId] [--end-tick N] [--seed N]");
        Console.WriteLine("  bisect <artifactsRoot> [runId] [--end-tick N] [--seed N]");
        Console.WriteLine("  event-stats <artifactsRoot> [runId] [topN] [fromTick] [toTick] [--kind K]...");
        Console.WriteLine("  timeline <artifactsRoot> [runId] [fromTick] [toTick] [maxPerTick] [maxPayloadChars] [--kind K]...");
        Console.WriteLine("  pretty-inspect <artifactsRoot> [runId]");
        Console.WriteLine("  list-runs <artifactsRoot> [limit]");
        Console.WriteLine("  inspect-run <artifactsRoot> <runId>");
        Console.WriteLine("  repro <artifactsRoot> [runId] [failureTick] [seedOverride]");
        Console.WriteLine("  list-adapters");
        Console.WriteLine();
        Console.WriteLine("Pass --help to any command for detailed usage.");
    }
}
