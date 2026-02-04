using System.Reflection;

namespace EpochSim.Cli.Domain;

public interface IDomainAdapterInvoker
{
    int Invoke<TState>(IDomainAdapter<TState> adapter);
}

public static class DomainAdapterDispatch
{
    private static readonly MethodInfo InvokeTypedMethod =
        typeof(DomainAdapterDispatch).GetMethod(nameof(InvokeTyped), BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("InvokeTyped not found.");

    public static int Invoke(IDomainAdapter adapter, IDomainAdapterInvoker invoker)
    {
        if (adapter is null) throw new ArgumentNullException(nameof(adapter));
        if (invoker is null) throw new ArgumentNullException(nameof(invoker));

        var stateType = adapter.StateType;
        var method = InvokeTypedMethod.MakeGenericMethod(stateType);
        return (int)method.Invoke(null, new object?[] { adapter, invoker })!;
    }

    private static int InvokeTyped<TState>(IDomainAdapter adapter, IDomainAdapterInvoker invoker)
    {
        if (adapter is not IDomainAdapter<TState> typed)
            throw new InvalidOperationException($"Adapter type mismatch. Expected IDomainAdapter<{typeof(TState).Name}>.");

        return invoker.Invoke(typed);
    }
}
