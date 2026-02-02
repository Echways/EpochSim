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

        if (!File.Exists(paths.EventsPath))
            throw new FileNotFoundException($"events.jsonl not found: {paths.EventsPath}");

        var meta = RunMetaReader.Read(paths.MetaPath);
        var endFromMeta = RunMetaReader.TryGetLong(meta, "endTick", out var em) ? em : 500;

        var pos = runParse.NextIndex;

        long? from = null;
        long? to = null;

        if (args.Length > pos && !CliParsing.IsOption(args[pos]) && CliParsing.TryParseLong(args[pos], out var f))
        {
            from = f;
            pos++;
        }

        if (args.Length > pos && !CliParsing.IsOption(args[pos]) && CliParsing.TryParseLong(args[pos], out var t))
        {
            to = t;
            pos++;
        }

        var maxPerTick = 50;
        var maxPayload = 120;

        if (args.Length > pos && !CliParsing.IsOption(args[pos]) && CliParsing.TryParseInt(args[pos], out var mpt))
        {
            maxPerTick = mpt;
            pos++;
        }

        if (args.Length > pos && !CliParsing.IsOption(args[pos]) && CliParsing.TryParseInt(args[pos], out var mp))
        {
            maxPayload = mp;
            pos++;
        }

        var kindsFilter = CliParsing.ParseKindsOptions(args, pos);

        var formatter = new CompositeEventPayloadFormatter(
            new PopulationEventPayloadFormatter(),
            new JsonEventPayloadFormatter());

        Console.WriteLine($"RunDir={paths.RunDir}");
        Console.WriteLine($"RunId={paths.RunId}");
        Console.WriteLine();

        var from2 = from ?? Math.Max(0, endFromMeta - 50);
        var to2 = to ?? endFromMeta;

        TimelineDumper.Dump(paths.EventsPath, from2, to2, maxPerTick, maxPayload, formatter, kindsFilter);

        return 0;
    }
}
