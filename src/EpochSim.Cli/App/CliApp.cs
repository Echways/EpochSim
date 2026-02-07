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
            ["repro"] = new ReproCommand()
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
        Console.WriteLine("  run <artifactsRoot> [runId] [endTick] [snapEvery] [seed] [--guard-state] [--compress] [--fingerprint-every N] [--snapshot-every N] [--max-pump-steps N] [--max-events-per-tick N] [--rng-version v1|v2] [--cancel-after-ms N]");
        Console.WriteLine("  validate-run <artifactsRoot> [runId] [endTick] [snapEvery] [seed] [--guard-state] [--compress] [--fingerprint-every N] [--snapshot-every N] [--max-pump-steps N] [--max-events-per-tick N] [--rng-version v1|v2] [--cancel-after-ms N]");
        Console.WriteLine("  fast-replay <artifactsRoot> [runIdOrRunDirOrEmptyForLatestWithEvents] [endTick] [ignored] [seed]");
        Console.WriteLine("  verify-run <artifactsRoot> [runIdOrEmptyForLatestWithEvents] [endTick] [ignored] [seed]");
        Console.WriteLine("  event-stats <artifactsRoot> [runId?] [topN?] [fromTick?] [toTick?] [--only K|--kind K]...");
        Console.WriteLine("  timeline <artifactsRoot> [runId?] [fromTick?] [toTick?] [maxPerTick?] [maxPayloadChars?] [--only K|--kind K]...");
        Console.WriteLine("  pretty-inspect <artifactsRoot> [runIdOrRunDirOrEmptyForLatest]");
        Console.WriteLine("  list-runs <artifactsRoot> [limit]");
        Console.WriteLine("  inspect-run <artifactsRoot> <runIdOrRunDir>");
        Console.WriteLine("  bisect <artifactsRoot> [runIdOrRunDirOrEmptyForLatestWithEvents] [endTick] [ignored] [seed]");
        Console.WriteLine("  repro <artifactsRoot> [runIdOrEmptyForLatestWithEvents] [failureTickOrEmptyForLatest] [seedOverride]");
    }
}
