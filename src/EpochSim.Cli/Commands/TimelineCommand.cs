using EpochSim.Cli.App;
using EpochSim.Cli.Parsing;
using EpochSim.Execution.Diagnostics;
using EpochSim.Execution.RunArtifacts;
using EpochSim.Samples.Population;

namespace EpochSim.Cli.Commands;

public sealed class TimelineCommand : ICliCommand
{
    public int Execute(CommandContext ctx, string[] args)
    {
        var runParse = CliParsing.ParseRunIdIfPresent(args);
        var runId = runParse.RunId ?? CliParsing.ResolveRunIdWithEvents(ctx.Root, "");
        var paths = CliParsing.Paths(ctx.Root, runId);

        var eventsPath = paths.ResolveEventsPath();
        if (!File.Exists(eventsPath))
            throw new FileNotFoundException($"events log not found: {eventsPath}");

        var meta = RunMetaReader.Read(paths.MetaPath);
        var manifest = RunManifestReader.TryRead(paths.ManifestPath);
        var endFromMeta = RunMetaReader.TryGetLong(meta, "endTick", out var em) ? em : 500;
        var endFromManifest = manifest?.EndTick ?? endFromMeta;

        var argIndex = runParse.NextIndex;

        long? fromTick = null;
        long? toTick = null;

        if (args.Length > argIndex && !CliParsing.IsOption(args[argIndex]) && CliParsing.TryParseLong(args[argIndex], out var f))
        {
            fromTick = f;
            argIndex++;
        }

        if (args.Length > argIndex && !CliParsing.IsOption(args[argIndex]) && CliParsing.TryParseLong(args[argIndex], out var t))
        {
            toTick = t;
            argIndex++;
        }

        var maxPerTick = 50;
        var maxPayload = 120;

        if (args.Length > argIndex && !CliParsing.IsOption(args[argIndex]) && CliParsing.TryParseInt(args[argIndex], out var mpt))
        {
            maxPerTick = mpt;
            argIndex++;
        }

        if (args.Length > argIndex && !CliParsing.IsOption(args[argIndex]) && CliParsing.TryParseInt(args[argIndex], out var mp))
        {
            maxPayload = mp;
            argIndex++;
        }

        var kindsFilter = CliParsing.ParseKindsOptions(args, argIndex);

        var formatter = new CompositeEventPayloadFormatter(
            new PopulationEventPayloadFormatter(),
            new JsonEventPayloadFormatter());

        Console.WriteLine($"RunDir={paths.RunDir}");
        Console.WriteLine($"RunId={paths.RunId}");
        Console.WriteLine();

        var resolvedFrom = fromTick ?? Math.Max(0, endFromManifest - 50);
        var resolvedTo = toTick ?? endFromManifest;

        TimelineDumper.Dump(eventsPath, resolvedFrom, resolvedTo, maxPerTick, maxPayload, formatter, kindsFilter);

        return 0;
    }
}
