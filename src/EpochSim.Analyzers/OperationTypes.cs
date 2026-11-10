using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace EpochSim.Analyzers;

internal static class OperationTypes
{
    public static string MetadataName(INamedTypeSymbol type)
        => type.ContainingNamespace is { IsGlobalNamespace: false } containingNamespace
            ? containingNamespace.ToDisplayString() + "." + type.MetadataName
            : type.MetadataName;

    public static IOperation Unwrap(IOperation operation)
    {
        while (operation is IConversionOperation conversion)
            operation = conversion.Operand;

        return operation;
    }

    public static INamedTypeSymbol? CollectionType(IOperation collection)
        => Unwrap(collection).Type as INamedTypeSymbol;
}
