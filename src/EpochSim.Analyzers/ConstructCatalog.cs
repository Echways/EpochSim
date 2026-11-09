using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace EpochSim.Analyzers;

internal static class ConstructCatalog
{
    private const string WallClockHint = "Use the simulation tick (ctx.Time) instead of wall-clock time.";
    private const string ConcurrencyHint = "Simulation code must run on the simulation thread; remove the concurrency.";
    private const string ExternalHint = "Pass external data into the simulation through commands or state.";
    private const string HashHint = "String and object hash codes differ between processes; use a stable hash.";
    private const string ConcurrentOrderHint = "Enumeration order of concurrent collections is not deterministic; use an ordered collection.";
    private const string HashCollectionHint = "Enumeration order depends on the history of inserts and removals; sort before iterating.";

    private static readonly HashSet<string> WallClockProperties = new()
    {
        "System.DateTime.Now",
        "System.DateTime.UtcNow",
        "System.DateTime.Today",
        "System.DateTimeOffset.Now",
        "System.DateTimeOffset.UtcNow",
        "System.Environment.TickCount",
        "System.Environment.TickCount64",
        "System.TimeProvider.System"
    };

    private static readonly HashSet<string> WallClockMethods = new()
    {
        "System.Diagnostics.Stopwatch.StartNew",
        "System.Diagnostics.Stopwatch.GetTimestamp"
    };

    private static readonly HashSet<string> ConcurrencyTypes = new()
    {
        "System.Threading.ThreadPool",
        "System.Threading.Tasks.Parallel"
    };

    private static readonly HashSet<string> ConcurrencyMethods = new()
    {
        "System.Threading.Tasks.Task.Run",
        "System.Linq.ParallelEnumerable.AsParallel"
    };

    private static readonly HashSet<string> TaskFactories = new()
    {
        "System.Threading.Tasks.TaskFactory",
        "System.Threading.Tasks.TaskFactory`1"
    };

    private static readonly HashSet<string> ExternalTypes = new()
    {
        "System.IO.File",
        "System.IO.Directory",
        "System.Net.Http.HttpClient"
    };

    private static readonly HashSet<string> ExternalMethods = new()
    {
        "System.Environment.GetEnvironmentVariable",
        "System.Environment.GetEnvironmentVariables"
    };

    private static readonly HashSet<string> ConcurrentCollections = new()
    {
        "System.Collections.Concurrent.ConcurrentDictionary`2",
        "System.Collections.Concurrent.ConcurrentBag`1",
        "System.Collections.Concurrent.ConcurrentQueue`1"
    };

    private static readonly HashSet<string> HashCollections = new()
    {
        "System.Collections.Generic.Dictionary`2",
        "System.Collections.Generic.HashSet`1"
    };

    public static Finding? Inspect(IOperation operation)
        => operation switch
        {
            IPropertyReferenceOperation property => InspectProperty(property),
            IInvocationOperation invocation => InspectInvocation(invocation),
            IObjectCreationOperation creation => InspectCreation(creation),
            IForEachLoopOperation loop => InspectEnumeration(loop.Collection, operation),
            IAwaitOperation => Create(Rules.Concurrency, "await", ConcurrencyHint, operation.Syntax.GetLocation()),
            IAnonymousFunctionOperation { Symbol.IsAsync: true } lambda => InspectAsyncLambda(lambda),
            _ => null
        };

    private static Finding? InspectProperty(IPropertyReferenceOperation operation)
    {
        var property = operation.Property;
        var typeName = OperationTypes.MetadataName(property.ContainingType);
        var key = typeName + "." + property.Name;
        var display = property.ContainingType.Name + "." + property.Name;
        var location = operation.Syntax.GetLocation();

        if (WallClockProperties.Contains(key))
            return Create(Rules.WallClock, display, WallClockHint, location);

        if (key == "System.Random.Shared")
            return Create(Rules.Randomness, display, "Use ctx.Rng instead of " + display, location);

        if (key == "System.Environment.MachineName")
            return Create(Rules.ExternalInput, display, ExternalHint, location);

        return null;
    }

    private static Finding? InspectInvocation(IInvocationOperation operation)
    {
        var method = operation.TargetMethod;
        var typeName = OperationTypes.MetadataName(method.ContainingType);
        var key = typeName + "." + method.Name;
        var display = method.ContainingType.Name + "." + method.Name;
        var location = operation.Syntax.GetLocation();

        if (WallClockMethods.Contains(key))
            return Create(Rules.WallClock, display, WallClockHint, location);

        if (key == "System.Guid.NewGuid" || typeName == "System.Security.Cryptography.RandomNumberGenerator")
            return Create(Rules.Randomness, display, "Use ctx.Rng instead of " + display, location);

        if (ConcurrencyMethods.Contains(key)
            || ConcurrencyTypes.Contains(typeName)
            || (TaskFactories.Contains(typeName) && method.Name == "StartNew"))
            return Create(Rules.Concurrency, display, ConcurrencyHint, location);

        if (ExternalTypes.Contains(typeName) || ExternalMethods.Contains(key))
            return Create(Rules.ExternalInput, display, ExternalHint, location);

        if (method.Name == "GetHashCode"
            && method.Parameters.IsEmpty
            && method.ContainingType.SpecialType is SpecialType.System_String or SpecialType.System_Object)
            return Create(Rules.Hashing, display, HashHint, location);

        if (typeName == "System.Linq.Enumerable" && !operation.Arguments.IsEmpty)
            return InspectEnumeration(operation.Arguments[0].Value, operation);

        return null;
    }

    private static Finding? InspectCreation(IObjectCreationOperation operation)
    {
        if (operation.Type is not INamedTypeSymbol type)
            return null;

        var display = "new " + type.Name;
        var location = operation.Syntax.GetLocation();

        return OperationTypes.MetadataName(type) switch
        {
            "System.Random" => Create(Rules.Randomness, display, "Use ctx.Rng instead of " + display, location),
            "System.Diagnostics.Stopwatch" => Create(Rules.WallClock, display, WallClockHint, location),
            "System.Threading.Thread" => Create(Rules.Concurrency, display, ConcurrencyHint, location),
            "System.Net.Http.HttpClient" => Create(Rules.ExternalInput, display, ExternalHint, location),
            _ => null
        };
    }

    private static Finding? InspectEnumeration(IOperation collection, IOperation site)
    {
        if (OperationTypes.CollectionType(collection) is not { } type)
            return null;

        var typeName = OperationTypes.MetadataName(type.OriginalDefinition);
        var location = site.Syntax.GetLocation();

        if (ConcurrentCollections.Contains(typeName))
            return Create(Rules.Hashing, type.Name, ConcurrentOrderHint, location);

        if (HashCollections.Contains(typeName))
            return Create(Rules.HashCollectionOrder, type.Name, HashCollectionHint, location);

        return null;
    }

    private static Finding? InspectAsyncLambda(IAnonymousFunctionOperation operation)
    {
        var location = operation.Syntax is AnonymousFunctionExpressionSyntax function
            ? function.AsyncKeyword.GetLocation()
            : operation.Syntax.GetLocation();

        return Create(Rules.Concurrency, "async lambda", ConcurrencyHint, location);
    }

    private static Finding Create(DiagnosticDescriptor rule, string construct, string hint, Location location)
        => new(rule, construct, hint, location);
}
