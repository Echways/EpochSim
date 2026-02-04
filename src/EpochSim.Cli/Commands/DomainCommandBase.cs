using EpochSim.Cli.App;
using EpochSim.Cli.Domain;

namespace EpochSim.Cli.Commands;

public abstract class DomainCommandBase : ICliCommand
{
    public int Execute(CommandContext ctx, string[] args)
        => DomainAdapterDispatch.Invoke(ctx.Adapter, new Invoker(this, ctx, args));

    protected abstract int Execute<TState>(IDomainAdapter<TState> adapter, CommandContext ctx, string[] args);

    private sealed class Invoker(DomainCommandBase command, CommandContext ctx, string[] args) : IDomainAdapterInvoker
    {
        public int Invoke<TState>(IDomainAdapter<TState> adapter)
            => command.Execute(adapter, ctx, args);
    }
}
