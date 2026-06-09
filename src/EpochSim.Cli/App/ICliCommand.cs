namespace EpochSim.Cli.App;

public interface ICliCommand
{
    int Execute(CommandContext ctx, string[] args);

    string Help => "No help available. Run without arguments to see usage.";
}
