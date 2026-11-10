using System;
using System.Collections.Concurrent;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace EpochSim.Analyzers;

internal sealed class SimulationScope
{
    private readonly INamedTypeSymbol? _engine;
    private readonly INamedTypeSymbol? _system;
    private readonly INamedTypeSymbol? _commandHandler;
    private readonly INamedTypeSymbol? _invariant;
    private readonly INamedTypeSymbol? _deterministic;
    private readonly ConcurrentDictionary<INamedTypeSymbol, bool> _simulationTypes = new(SymbolEqualityComparer.Default);

    private SimulationScope(Compilation compilation)
    {
        _engine = compilation.GetTypeByMetadataName("EpochSim.Execution.SimulationEngine`1");
        _system = compilation.GetTypeByMetadataName("EpochSim.Kernel.Systems.ISystem`1");
        _commandHandler = compilation.GetTypeByMetadataName("EpochSim.Kernel.Messaging.ICommandHandler`2");
        _invariant = compilation.GetTypeByMetadataName("EpochSim.Kernel.Validation.IInvariant`1");
        _deterministic = compilation.GetTypeByMetadataName("EpochSim.Kernel.Determinism.DeterministicAttribute");
    }

    public static SimulationScope? Create(Compilation compilation)
    {
        var scope = new SimulationScope(compilation);
        return scope.IsRelevant ? scope : null;
    }

    private bool IsRelevant
        => _engine is not null || _system is not null || _commandHandler is not null
           || _invariant is not null || _deterministic is not null;

    public bool IsRegistration(IMethodSymbol method)
        => _engine is not null
           && SymbolEqualityComparer.Default.Equals(method.ContainingType?.OriginalDefinition, _engine)
           && method.Name is "AddSystem" or "OnCommand";

    public bool IsSimulationCode(IOperation operation, ISymbol containingSymbol)
    {
        for (var current = operation; current is not null; current = current.Parent)
        {
            if (current is IAnonymousFunctionOperation lambda && IsRegisteredLambda(lambda))
                return true;
        }

        for (var symbol = containingSymbol; symbol is not null; symbol = symbol.ContainingSymbol)
        {
            if (symbol is IMethodSymbol method && HasDeterministicAttribute(method))
                return true;

            if (symbol is INamedTypeSymbol type && IsSimulationType(type))
                return true;
        }

        return false;
    }

    private bool IsRegisteredLambda(IAnonymousFunctionOperation lambda)
    {
        var parent = lambda.Parent;
        while (parent is IDelegateCreationOperation or IConversionOperation or IParenthesizedOperation)
            parent = parent.Parent;

        return parent is IArgumentOperation { Parent: IInvocationOperation invocation }
               && IsRegistration(invocation.TargetMethod);
    }

    private bool IsSimulationType(INamedTypeSymbol type)
        => _simulationTypes.GetOrAdd(type, candidate => HasDeterministicAttribute(candidate) || ImplementsSimulationInterface(candidate));

    private bool ImplementsSimulationInterface(INamedTypeSymbol type)
        => type.AllInterfaces.Any(candidate =>
        {
            var definition = candidate.OriginalDefinition;
            return SymbolEqualityComparer.Default.Equals(definition, _system)
                   || SymbolEqualityComparer.Default.Equals(definition, _commandHandler)
                   || SymbolEqualityComparer.Default.Equals(definition, _invariant);
        });

    private bool HasDeterministicAttribute(ISymbol symbol)
        => _deterministic is not null
           && symbol.GetAttributes().Any(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, _deterministic));

    public static IMethodSymbol? ReferencedMethod(IOperation argumentValue)
    {
        var current = argumentValue;
        while (current is IDelegateCreationOperation or IConversionOperation or IParenthesizedOperation)
        {
            current = current switch
            {
                IDelegateCreationOperation creation => creation.Target,
                IConversionOperation conversion => conversion.Operand,
                IParenthesizedOperation parenthesized => parenthesized.Operand,
                _ => current
            };
        }

        return (current as IMethodReferenceOperation)?.Method.OriginalDefinition;
    }
}
