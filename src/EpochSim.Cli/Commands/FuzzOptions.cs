using EpochSim.Cli.Parsing;

namespace EpochSim.Cli.Commands;

internal sealed record FuzzOptions(
    string? AdapterName,
    ulong FromSeed,
    ulong ToSeed,
    long Ticks,
    bool CheckDeterminism,
    int? Parallelism,
    string OutputDirectory,
    bool Shrink)
{
    private const ulong DefaultFromSeed = 1;
    private const ulong DefaultToSeed = 1000;
    private const long DefaultTicks = 500;
    private const string DefaultOutputDirectory = "artifacts";

    public static FuzzOptions Parse(string[] args)
    {
        var options = new FuzzOptions(null, DefaultFromSeed, DefaultToSeed, DefaultTicks, false, null, DefaultOutputDirectory, true);

        for (var i = 0; i < args.Length; i++)
        {
            var option = args[i].ToLowerInvariant();

            options = option switch
            {
                "--adapter" => options with { AdapterName = ReadValue(args, ref i, option) },
                "--seeds" => WithSeeds(options, ReadValue(args, ref i, option)),
                "--ticks" => options with { Ticks = ParseTicks(ReadValue(args, ref i, option)) },
                "--check-determinism" => options with { CheckDeterminism = true },
                "--parallel" => options with { Parallelism = ParseParallelism(ReadValue(args, ref i, option)) },
                "--out" => options with { OutputDirectory = ReadValue(args, ref i, option) },
                "--no-shrink" => options with { Shrink = false },
                _ => throw new ArgumentException($"Unknown argument '{args[i]}'. Run 'fuzz --help' for usage.")
            };
        }

        return options;
    }

    public string ReproCommand(string adapterName, ulong seed, long tick, bool checkDeterminism)
    {
        var command = $"epochsim fuzz --adapter {adapterName} --seeds {seed} --ticks {tick} --out {OutputDirectory}";
        return checkDeterminism ? $"{command} --check-determinism" : command;
    }

    private static string ReadValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length || CliParsing.IsOption(args[index + 1]))
            throw new ArgumentException($"Option {option} requires a value.");

        index++;
        return args[index];
    }

    private static FuzzOptions WithSeeds(FuzzOptions options, string value)
    {
        var parts = value.Split("..", StringSplitOptions.None);

        if (parts.Length is < 1 or > 2
            || !CliParsing.TryParseUlong(parts[0], out var from)
            || !CliParsing.TryParseUlong(parts[^1], out var to)
            || from > to)
            throw new ArgumentException($"Invalid --seeds value '{value}'. Expected N or A..B with A <= B.");

        return options with { FromSeed = from, ToSeed = to };
    }

    private static long ParseTicks(string value)
        => CliParsing.TryParseLong(value, out var ticks) && ticks >= 0
            ? ticks
            : throw new ArgumentException($"Invalid --ticks value '{value}'. Expected a non-negative integer.");

    private static int ParseParallelism(string value)
        => CliParsing.TryParseInt(value, out var degree) && degree >= 1
            ? degree
            : throw new ArgumentException($"Invalid --parallel value '{value}'. Expected an integer >= 1.");
}
