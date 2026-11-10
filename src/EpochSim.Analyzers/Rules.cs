using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace EpochSim.Analyzers;

internal static class Rules
{
    public const string RandomnessId = "ESIM002";

    private const string Category = "EpochSim.Determinism";
    private const string HelpLink = "https://github.com/Echways/EpochSim/blob/main/docs/en/determinism.md#analyzer";
    private const string MessageFormat = "'{0}' breaks determinism in simulation code. {1}";

    public static readonly DiagnosticDescriptor WallClock = Create(
        "ESIM001", "Wall-clock time in simulation code", DiagnosticSeverity.Warning);

    public static readonly DiagnosticDescriptor Randomness = Create(
        RandomnessId, "Unseeded randomness in simulation code", DiagnosticSeverity.Warning);

    public static readonly DiagnosticDescriptor Concurrency = Create(
        "ESIM003", "Concurrency in simulation code", DiagnosticSeverity.Warning);

    public static readonly DiagnosticDescriptor Hashing = Create(
        "ESIM004", "Process-dependent hashing or unordered enumeration in simulation code", DiagnosticSeverity.Warning);

    public static readonly DiagnosticDescriptor ExternalInput = Create(
        "ESIM005", "File, network or environment access in simulation code", DiagnosticSeverity.Warning);

    public static readonly DiagnosticDescriptor HashCollectionOrder = Create(
        "ESIM006", "Enumeration of Dictionary or HashSet in simulation code", DiagnosticSeverity.Hidden);

    public static readonly ImmutableArray<DiagnosticDescriptor> All = ImmutableArray.Create(
        WallClock, Randomness, Concurrency, Hashing, ExternalInput, HashCollectionOrder);

    private static DiagnosticDescriptor Create(string id, string title, DiagnosticSeverity severity)
        => new(
            id,
            title,
            MessageFormat,
            Category,
            severity,
            isEnabledByDefault: true,
            description: title,
            helpLinkUri: HelpLink);
}
