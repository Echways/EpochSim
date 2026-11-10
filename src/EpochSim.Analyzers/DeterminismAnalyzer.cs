using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace EpochSim.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DeterminismAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => Rules.All;

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext start)
    {
        var scope = SimulationScope.Create(start.Compilation);
        if (scope is null)
            return;

        var methodGroups = new MethodGroupFindings();

        start.RegisterOperationAction(
            context => Analyze(context, scope, methodGroups),
            OperationKind.PropertyReference,
            OperationKind.Invocation,
            OperationKind.ObjectCreation,
            OperationKind.Await,
            OperationKind.AnonymousFunction,
            OperationKind.Loop);

        start.RegisterCompilationEndAction(context =>
        {
            foreach (var diagnostic in methodGroups.Resolve())
                context.ReportDiagnostic(diagnostic);
        });
    }

    private static void Analyze(OperationAnalysisContext context, SimulationScope scope, MethodGroupFindings methodGroups)
    {
        if (context.Operation is IInvocationOperation invocation && scope.IsRegistration(invocation.TargetMethod))
            methodGroups.Register(invocation);

        if (ConstructCatalog.Inspect(context.Operation) is not { } finding)
            return;

        var diagnostic = finding.ToDiagnostic();

        if (scope.IsSimulationCode(context.Operation, context.ContainingSymbol))
            context.ReportDiagnostic(diagnostic);
        else if (context.ContainingSymbol is IMethodSymbol method)
            methodGroups.Defer(method, diagnostic);
    }
}
