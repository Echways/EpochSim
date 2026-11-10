using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

internal static class SimulationSource
{
    public const string StatementMarker = "__STATEMENT__";

    private const string Usings =
        """
        using System;
        using System.Collections.Concurrent;
        using System.Collections.Generic;
        using System.Diagnostics;
        using System.IO;
        using System.Linq;
        using System.Net.Http;
        using System.Security.Cryptography;
        using System.Threading;
        using System.Threading.Tasks;
        using EpochSim.Execution;
        using EpochSim.Fuzzing;
        using EpochSim.Kernel.Determinism;
        using EpochSim.Kernel.Messaging;
        using EpochSim.Kernel.Systems;
        using EpochSim.Kernel.Time;
        using EpochSim.Kernel.Validation;

        public sealed class S { public int N; }
        public sealed record Cmd() : ICommand;

        """;

    public static readonly ReferenceAssemblies Net10 = new(
        "net10.0",
        new PackageIdentity("Microsoft.NETCore.App.Ref", "10.0.0"),
        Path.Combine("ref", "net10.0"));

    public static IEnumerable<MetadataReference> EpochSimReferences()
    {
        yield return MetadataReference.CreateFromFile(typeof(EpochSim.Epoch).Assembly.Location);
        yield return MetadataReference.CreateFromFile(typeof(EpochSim.Kernel.Determinism.IRng).Assembly.Location);
        yield return MetadataReference.CreateFromFile(typeof(EpochSim.Execution.SimulationEngine<>).Assembly.Location);
    }

    public static string Lambda(string statements)
        => Usings + $$"""
            public static class Host
            {
                public static void Configure(SimulationEngine<S> engine)
                {
                    engine.AddSystem("s", tick: ctx => { {{StatementMarker}} });
                }
            }
            """.Replace(StatementMarker, statements);

    public static string OnCommandLambda(string statements)
        => Usings + $$"""
            public static class Host
            {
                public static void Configure(SimulationEngine<S> engine)
                {
                    engine.OnCommand<Cmd>((state, command, events) => { {{StatementMarker}} });
                }
            }
            """.Replace(StatementMarker, statements);

    public static string StepLambda(string statements)
        => Usings + $$"""
            public static class Host
            {
                public static void Configure(FuzzBuilder<S> fuzz)
                {
                    fuzz.Step((state, ctx) => { {{StatementMarker}} });
                }
            }
            """.Replace(StatementMarker, statements);

    public static string InvariantLambda(string statements)
        => Usings + $$"""
            public static class Host
            {
                public static void Configure(FuzzBuilder<S> fuzz)
                {
                    fuzz.Invariant("inv", state => { {{StatementMarker}} return true; });
                }
            }
            """.Replace(StatementMarker, statements);

    public static string SystemClass(string statements)
        => Usings + $$"""
            public sealed class Sys : ISystem<S>
            {
                public void Tick(TickContext<S> ctx) { {{StatementMarker}} }
            }
            """.Replace(StatementMarker, statements);

    public static string InvariantClass(string statements)
        => Usings + $$"""
            public sealed class Inv : IInvariant<S>
            {
                public string Name => "inv";
                public bool Check(SimTime time, S state, out string message)
                {
                    {{StatementMarker}}
                    message = "";
                    return true;
                }
            }
            """.Replace(StatementMarker, statements);

    public static string CommandHandlerClass(string statements)
        => Usings + $$"""
            public sealed class Handler : ICommandHandler<S, Cmd>
            {
                public void Handle(S state, Cmd command, IEventBuffer events) { {{StatementMarker}} }
            }
            """.Replace(StatementMarker, statements);

    public static string DeterministicMethod(string statements)
        => Usings + $$"""
            public static class Helpers
            {
                [Deterministic]
                public static void Run() { {{StatementMarker}} }
            }
            """.Replace(StatementMarker, statements);

    public static string DeterministicClass(string statements)
        => Usings + $$"""
            [Deterministic]
            public static class Helpers
            {
                public static void Run() { {{StatementMarker}} }
            }
            """.Replace(StatementMarker, statements);

    public static string MethodGroup(string statements)
        => Usings + $$"""
            public static class Host
            {
                public static void Configure(SimulationEngine<S> engine)
                {
                    engine.AddSystem("s", Tick);
                }

                private static void Tick(TickContext<S> ctx) { {{StatementMarker}} }
            }
            """.Replace(StatementMarker, statements);

    public static string Outside(string statements)
        => Usings + $$"""
            public static class Plain
            {
                public static void Run() { {{StatementMarker}} }
            }
            """.Replace(StatementMarker, statements);

    public static IEnumerable<Func<string, string>> SimulationContexts { get; } =
    [
        Lambda,
        OnCommandLambda,
        StepLambda,
        InvariantLambda,
        SystemClass,
        InvariantClass,
        CommandHandlerClass,
        DeterministicMethod,
        DeterministicClass,
        MethodGroup
    ];
}
