using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EpochSim.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RandomToRngCodeFixProvider)), Shared]
public sealed class RandomToRngCodeFixProvider : CodeFixProvider
{
    private const string RandomnessId = "ESIM002";
    private const string TickContextName = "EpochSim.Kernel.Systems.TickContext`1";
    private const string EventContextName = "EpochSim.Kernel.Systems.EventContext`1";

    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(RandomnessId);

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null || model is null)
            return;

        var diagnostic = context.Diagnostics[0];
        var source = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

        if (!TryMatchCall(source, out var invocation, out var replacementMethod))
            return;

        var contextName = FindContextParameter(model, invocation, context.CancellationToken);
        if (contextName is null)
            return;

        var title = $"Use {contextName}.Rng.{replacementMethod}";
        context.RegisterCodeFix(
            CodeAction.Create(
                title,
                _ => Replace(context.Document, root, invocation, contextName, replacementMethod),
                equivalenceKey: title),
            diagnostic);
    }

    private static bool TryMatchCall(SyntaxNode source, out InvocationExpressionSyntax invocation, out string replacementMethod)
    {
        invocation = null!;
        replacementMethod = string.Empty;

        if (source.Parent is not MemberAccessExpressionSyntax access
            || access.Expression != source
            || access.Parent is not InvocationExpressionSyntax call
            || call.Expression != access)
            return false;

        var argumentCount = call.ArgumentList.Arguments.Count;

        if (access.Name.Identifier.ValueText == "Next" && argumentCount == 2)
            replacementMethod = "NextInt";
        else if (access.Name.Identifier.ValueText == "NextDouble" && argumentCount == 0)
            replacementMethod = "NextDouble01";
        else
            return false;

        invocation = call;
        return true;
    }

    private static string? FindContextParameter(SemanticModel model, SyntaxNode site, CancellationToken cancellationToken)
    {
        foreach (var ancestor in site.Ancestors())
        {
            if (ancestor is not (LambdaExpressionSyntax or AnonymousMethodExpressionSyntax or BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax))
                continue;

            var method = ancestor is LambdaExpressionSyntax or AnonymousMethodExpressionSyntax
                ? model.GetSymbolInfo(ancestor, cancellationToken).Symbol as IMethodSymbol
                : model.GetDeclaredSymbol(ancestor, cancellationToken) as IMethodSymbol;

            var parameter = method?.Parameters.FirstOrDefault(IsSimulationContext);
            if (parameter is not null)
                return parameter.Name;
        }

        return null;
    }

    private static bool IsSimulationContext(IParameterSymbol parameter)
    {
        if (parameter.Type is not INamedTypeSymbol { IsGenericType: true } type)
            return false;

        var definition = type.OriginalDefinition;
        var name = definition.ContainingNamespace.ToDisplayString() + "." + definition.MetadataName;
        return name == TickContextName || name == EventContextName;
    }

    private static Task<Document> Replace(
        Document document,
        SyntaxNode root,
        InvocationExpressionSyntax invocation,
        string contextName,
        string replacementMethod)
    {
        var rng = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            SyntaxFactory.IdentifierName(contextName),
            SyntaxFactory.IdentifierName("Rng"));

        var replacement = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    rng,
                    SyntaxFactory.IdentifierName(replacementMethod)),
                invocation.ArgumentList)
            .WithTriviaFrom(invocation);

        return Task.FromResult(document.WithSyntaxRoot(root.ReplaceNode(invocation, replacement)));
    }
}
