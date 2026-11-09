using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace EpochSim.Analyzers;

internal sealed class MethodGroupFindings
{
    private readonly ConcurrentDictionary<IMethodSymbol, byte> _registered = new(SymbolEqualityComparer.Default);
    private readonly ConcurrentDictionary<IMethodSymbol, ConcurrentBag<Diagnostic>> _pending = new(SymbolEqualityComparer.Default);

    public void Register(IInvocationOperation registration)
    {
        foreach (var argument in registration.Arguments)
        {
            if (SimulationScope.ReferencedMethod(argument.Value) is { } method)
                _registered.TryAdd(method, 0);
        }
    }

    public void Defer(IMethodSymbol method, Diagnostic diagnostic)
        => _pending.GetOrAdd(method.OriginalDefinition, _ => new ConcurrentBag<Diagnostic>()).Add(diagnostic);

    public IEnumerable<Diagnostic> Resolve()
    {
        foreach (var method in _registered.Keys)
        {
            if (!_pending.TryGetValue(method, out var diagnostics))
                continue;

            foreach (var diagnostic in diagnostics)
                yield return diagnostic;
        }
    }
}
