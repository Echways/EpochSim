namespace EpochSim.Cli.App;

public interface ICliCommand
{
    int Execute(CommandContext ctx, string[] args);
}