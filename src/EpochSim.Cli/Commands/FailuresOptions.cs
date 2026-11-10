using EpochSim.Cli.Parsing;
using EpochSim.Fuzzing;

namespace EpochSim.Cli.Commands;

internal sealed record FailuresOptions(
    string StorePath,
    string? Framework,
    string? Configure,
    string? OutputDirectory,
    string? SuiteName,
    string? StateType)
{
    public static FailuresOptions Parse(string[] args)
    {
        var options = new FailuresOptions(FuzzFailureStore.DefaultPath, null, null, null, null, null);

        for (var i = 0; i < args.Length; i++)
        {
            var option = args[i].ToLowerInvariant();

            options = option switch
            {
                "--store" => options with { StorePath = ReadValue(args, ref i, option) },
                "--framework" => options with { Framework = ReadValue(args, ref i, option) },
                "--configure" => options with { Configure = ReadValue(args, ref i, option) },
                "--out" => options with { OutputDirectory = ReadValue(args, ref i, option) },
                "--name" => options with { SuiteName = ReadValue(args, ref i, option) },
                "--state-type" => options with { StateType = ReadValue(args, ref i, option) },
                _ => throw new ArgumentException($"Unknown argument '{args[i]}'. Run 'failures --help' for usage.")
            };
        }

        return options;
    }

    private static string ReadValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length || CliParsing.IsOption(args[index + 1]))
            throw new ArgumentException($"Option {option} requires a value.");

        index++;
        return args[index];
    }
}
