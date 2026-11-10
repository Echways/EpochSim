using EpochSim.Analyzers;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

internal static class AnalyzerVerifier
{
    public static Task Verify(string source, string? editorConfig = null, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<DeterminismAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = SimulationSource.Net10
        };

        test.TestState.AdditionalReferences.AddRange(SimulationSource.EpochSimReferences());
        test.ExpectedDiagnostics.AddRange(expected);

        if (editorConfig is not null)
            test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", editorConfig));

        return test.RunAsync();
    }

    public static Task VerifyNoFix(string markedSource)
        => VerifyFix(markedSource, markedSource, MarkupMode.Allow);

    public static Task VerifyFix(string source, string fixedSource, MarkupMode fixedMarkup = MarkupMode.None)
    {
        var test = new CSharpCodeFixTest<DeterminismAnalyzer, RandomToRngCodeFixProvider, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedSource,
            ReferenceAssemblies = SimulationSource.Net10
        };

        test.FixedState.MarkupHandling = fixedMarkup;
        test.TestState.AdditionalReferences.AddRange(SimulationSource.EpochSimReferences());
        test.FixedState.AdditionalReferences.AddRange(SimulationSource.EpochSimReferences());
        return test.RunAsync();
    }
}
