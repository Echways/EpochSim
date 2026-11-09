using Microsoft.CodeAnalysis;

namespace EpochSim.Analyzers;

internal readonly struct Finding(DiagnosticDescriptor rule, string construct, string hint, Location location)
{
    public Diagnostic ToDiagnostic()
        => Diagnostic.Create(rule, location, construct, hint);
}
