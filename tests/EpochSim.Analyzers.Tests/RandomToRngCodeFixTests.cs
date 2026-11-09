public sealed class RandomToRngCodeFixTests
{
    [Fact]
    public Task Replaces_SharedRandomNext_InSystemLambda()
        => AnalyzerVerifier.VerifyFix(
            SimulationSource.Lambda("var v = {|ESIM002:Random.Shared|}.Next(1, 10);"),
            SimulationSource.Lambda("var v = ctx.Rng.NextInt(1, 10);"));

    [Fact]
    public Task Replaces_NewRandomNextDouble_InSystemLambda()
        => AnalyzerVerifier.VerifyFix(
            SimulationSource.Lambda("var v = {|ESIM002:new Random(5)|}.NextDouble();"),
            SimulationSource.Lambda("var v = ctx.Rng.NextDouble01();"));

    [Fact]
    public Task UsesActualParameterName_InSystemMethod()
        => AnalyzerVerifier.VerifyFix(
            SimulationSource.SystemClass("").Replace(
                "public void Tick(TickContext<S> ctx) {  }",
                "public void Tick(TickContext<S> context) { var v = {|ESIM002:Random.Shared|}.Next(2, 4); }"),
            SimulationSource.SystemClass("").Replace(
                "public void Tick(TickContext<S> ctx) {  }",
                "public void Tick(TickContext<S> context) { var v = context.Rng.NextInt(2, 4); }"));

    [Fact]
    public Task Replaces_InEventContextHandler()
        => AnalyzerVerifier.VerifyFix(
            SimulationSource.SystemClass("").Replace(
                "public void Tick(TickContext<S> ctx) {  }",
                "public void Tick(TickContext<S> ctx) { }\n    public void Handle(EventContext<S> events, IEvent ev) { var v = {|ESIM002:Random.Shared|}.Next(0, 3); }"),
            SimulationSource.SystemClass("").Replace(
                "public void Tick(TickContext<S> ctx) {  }",
                "public void Tick(TickContext<S> ctx) { }\n    public void Handle(EventContext<S> events, IEvent ev) { var v = events.Rng.NextInt(0, 3); }"));

    [Fact]
    public Task Replaces_InNestedLambdaUsingOuterContext()
        => AnalyzerVerifier.VerifyFix(
            SimulationSource.Lambda("Func<int> f = () => {|ESIM002:Random.Shared|}.Next(1, 3);"),
            SimulationSource.Lambda("Func<int> f = () => ctx.Rng.NextInt(1, 3);"));

    [Fact]
    public Task OffersNoFix_ForUnsupportedRandomCalls()
    {
        return AnalyzerVerifier.VerifyNoFix(SimulationSource.Lambda("var v = {|ESIM002:Random.Shared|}.Next(10);"));
    }

    [Fact]
    public Task OffersNoFix_ForGuid()
    {
        return AnalyzerVerifier.VerifyNoFix(SimulationSource.Lambda("var v = {|ESIM002:Guid.NewGuid()|};"));
    }

    [Fact]
    public Task OffersNoFix_WithoutContextInScope()
    {
        return AnalyzerVerifier.VerifyNoFix(SimulationSource.DeterministicMethod("var v = {|ESIM002:Random.Shared|}.Next(1, 4);"));
    }
}
