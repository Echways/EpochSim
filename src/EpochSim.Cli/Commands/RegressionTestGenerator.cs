using System.Text;
using System.Text.RegularExpressions;
using EpochSim.Fuzzing;

namespace EpochSim.Cli.Commands;

internal static partial class RegressionTestGenerator
{
    private const string Indent = "    ";

    public static string ClassName(ConfigureMethod configure)
        => $"{configure.TypeName}RegressionTests";

    public static string Generate(
        RegressionTestFramework framework,
        ConfigureMethod configure,
        string stateType,
        IReadOnlyList<FuzzStoredFailure> failures)
    {
        var text = new StringBuilder();

        foreach (var line in new[] { "EpochSim.Testing", framework.NamespaceUsing }.Order(StringComparer.Ordinal))
            text.Append("using ").Append(line).AppendLine(";");

        text.AppendLine();

        if (configure.Namespace.Length > 0)
            text.Append("namespace ").Append(configure.Namespace).AppendLine(";").AppendLine();

        if (framework.ClassAttribute is { } classAttribute)
            text.Append('[').Append(classAttribute).AppendLine("]");

        text.Append("public sealed class ").AppendLine(ClassName(configure));
        text.AppendLine("{");

        var usedNames = new HashSet<string>(StringComparer.Ordinal);
        var methods = failures.Select(failure => Method(framework, configure, stateType, failure, UniqueName(failure, usedNames)));
        text.AppendJoin(Environment.NewLine + Environment.NewLine, methods);

        text.AppendLine().AppendLine("}");
        return text.ToString().ReplaceLineEndings("\n");
    }

    private static string Method(
        RegressionTestFramework framework,
        ConfigureMethod configure,
        string stateType,
        FuzzStoredFailure failure,
        string methodName)
    {
        var text = new StringBuilder();
        text.Append(Indent).Append('[').Append(framework.MethodAttribute).AppendLine("]");
        text.Append(Indent).Append("public void ").Append(methodName).AppendLine("()");
        text.Append(Indent).AppendLine("{");
        text.Append(Indent).Append(Indent).Append(Call(configure, stateType, failure)).AppendLine();
        text.Append(Indent).Append('}');
        return text.ToString();
    }

    private static string Call(ConfigureMethod configure, string stateType, FuzzStoredFailure failure)
    {
        var target = failure.Kind == FuzzFailureKind.NonDeterminism
            ? $"fuzz => {{ {configure.FullName}(fuzz); fuzz.CheckDeterminism(); }}"
            : configure.FullName;

        var arguments = $"{target}, {failure.Seed}, {failure.ReproTicks}";

        if (failure.CommandIndices is { } indices)
            arguments += $", [{string.Join(", ", indices)}]";

        return $"SimAssert.Regression<global::{stateType}>({arguments});";
    }

    private static string UniqueName(FuzzStoredFailure failure, HashSet<string> usedNames)
    {
        var identity = IdentifierCharacters().Replace(failure.Identity.Split('.')[^1], "_");
        var baseName = $"Seed{failure.Seed}_{(identity.Length > 0 ? identity : failure.Kind.ToString())}_AtTick{failure.Tick}";

        var name = baseName;
        for (var suffix = 2; !usedNames.Add(name); suffix++)
            name = $"{baseName}_{suffix}";

        return name;
    }

    [GeneratedRegex(@"\W+")]
    private static partial Regex IdentifierCharacters();
}
