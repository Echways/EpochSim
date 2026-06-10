using EpochSim.Cli.App;
using EpochSim.Cli.Domain;

namespace EpochSim.Cli.Commands;

public sealed class ListAdaptersCommand : ICliCommand
{
    public string Help =>
        """
        list-adapters

        Lists all domain adapters registered in the CLI.
        """;

    public int Execute(CommandContext ctx, string[] args)
    {
        Console.WriteLine("Registered domain adapters:");
        foreach (var name in DomainAdapterRegistry.Names)
            Console.WriteLine($"  {name}");
        return 0;
    }
}
