using EpochSim.Cli.App;
using EpochSim.Cli.Domain;
using EpochSim.Execution.RunArtifacts;

namespace EpochSim.Cli.Commands;

public abstract class DomainCommandBase : ICliCommand
{
    public int Execute(CommandContext ctx, string[] args)
        => DomainAdapterDispatch.Invoke(ctx.Adapter, new Invoker(this, ctx, args));

    protected abstract int Execute<TState>(IDomainAdapter<TState> adapter, CommandContext ctx, string[] args);

    protected static bool CheckDomain<TState>(IDomainAdapter<TState> adapter, string runDir)
    {
        var manifestPath = Path.Combine(runDir, RunPaths.ManifestFileName);
        var manifest = RunManifestReader.TryRead(manifestPath);

        if (manifest?.Domain is { } manifestDomain
            && !string.Equals(manifestDomain, adapter.Name, StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine(
                $"Domain mismatch: this run was created with domain '{manifestDomain}' " +
                $"but the CLI is loaded with domain '{adapter.Name}'. " +
                $"Use --adapter '{manifestDomain}' or --adapter-assembly to load the correct adapter, " +
                $"or run 'list-adapters' to see registered adapters.");
            return false;
        }

        return true;
    }

    private sealed class Invoker(DomainCommandBase command, CommandContext ctx, string[] args) : IDomainAdapterInvoker
    {
        public int Invoke<TState>(IDomainAdapter<TState> adapter)
            => command.Execute(adapter, ctx, args);
    }
}
