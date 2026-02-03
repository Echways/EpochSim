using EpochSim.Cli.App;
using EpochSim.Cli.Parsing;
using EpochSim.Execution.Diagnostics;

namespace EpochSim.Cli.Commands;

public sealed class EventStatsCommand : ICliCommand
{
    public int Execute(CommandContext ctx, string[] args)
    {
        var runParse = CliParsing.ParseRunIdIfPresent(args);
        var runId = runParse.RunId ?? CliParsing.ResolveRunIdWithEvents(ctx.Root, "");
        var paths = CliParsing.Paths(ctx.Root, runId);

        var argIndex = runParse.NextIndex;

        var topN = 20;
        long? fromTick = null;
        long? toTick = null;

        if (args.Length > argIndex && !CliParsing.IsOption(args[argIndex]) && CliParsing.TryParseInt(args[argIndex], out var topArg))
        {
            topN = topArg;
            argIndex++;
        }

        if (args.Length > argIndex && !CliParsing.IsOption(args[argIndex]) && CliParsing.TryParseLong(args[argIndex], out var fromArg))
        {
            fromTick = fromArg;
            argIndex++;
        }

        if (args.Length > argIndex && !CliParsing.IsOption(args[argIndex]) && CliParsing.TryParseLong(args[argIndex], out var toArg))
        {
            toTick = toArg;
            argIndex++;
        }

        var kindsFilter = CliParsing.ParseKindsOptions(args, argIndex);

        var eventsPath = paths.ResolveEventsPath();
        if (!File.Exists(eventsPath))
            throw new FileNotFoundException($"events log not found: {eventsPath}");

        var stats = EventLogStatsComputer.Compute(eventsPath, fromTick, toTick, topN, kindsFilter);

        Console.WriteLine($"RunDir={paths.RunDir}");
        Console.WriteLine($"RunId={paths.RunId}");
        Console.WriteLine($"EventsTotal={stats.TotalEvents}");
        Console.WriteLine($"TicksRange={stats.MinTick}..{stats.MaxTick}");
        Console.WriteLine($"Kinds={stats.ByKind.Count}");
        Console.WriteLine();

        Console.WriteLine($"TopKinds (top {Math.Min(topN, stats.TopKinds.Count)}):");
        foreach (var (k, c) in stats.TopKinds)
            Console.WriteLine($"  {k} = {c}");
        Console.WriteLine();

        Console.WriteLine($"TopTicks (top {Math.Min(topN, stats.TopTicks.Count)}):");
        foreach (var (t, c) in stats.TopTicks)
            Console.WriteLine($"  t={t} events={c}");
        Console.WriteLine();

        var selectedKinds = (kindsFilter is null || kindsFilter.Count == 0)
            ? stats.TopKinds.Select(x => x.Kind).Take(10).ToArray()
            : kindsFilter.ToArray();

        var anySmart = false;

        foreach (var k in selectedKinds)
        {
            if (!stats.IntFieldDistributions.TryGetValue(k, out var fields) || fields.Count == 0)
                continue;

            anySmart = true;
            Console.WriteLine($"Kind={k}");

            foreach (var (field, map) in fields.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                if (map.Count == 0) continue;

                var total = map.Values.Sum();
                var min = map.Keys.Min();
                var max = map.Keys.Max();

                Console.WriteLine($"  Field={field} Count={total} Range={min}..{max}");

                var topVals = map
                    .OrderByDescending(x => x.Value)
                    .ThenBy(x => x.Key)
                    .Take(12)
                    .ToArray();

                foreach (var (val, cnt) in topVals)
                    Console.WriteLine($"    {val} = {cnt}");

                if (map.Count > topVals.Length)
                    Console.WriteLine($"    ... +{map.Count - topVals.Length} more values");
            }

            Console.WriteLine();
        }

        if (!anySmart)
            Console.WriteLine("SmartFields: none (no int fields found in payloads for selected kinds)");

        return 0;
    }
}
