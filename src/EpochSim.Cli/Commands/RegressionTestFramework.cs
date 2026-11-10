namespace EpochSim.Cli.Commands;

internal sealed record RegressionTestFramework(
    string Name,
    string NamespaceUsing,
    string? ClassAttribute,
    string MethodAttribute)
{
    public static IReadOnlyList<RegressionTestFramework> All { get; } =
    [
        new("xunit", "Xunit", null, "Fact"),
        new("nunit", "NUnit.Framework", "TestFixture", "Test"),
        new("mstest", "Microsoft.VisualStudio.TestTools.UnitTesting", "TestClass", "TestMethod")
    ];

    public static RegressionTestFramework Parse(string? name)
        => All.FirstOrDefault(framework => string.Equals(framework.Name, name, StringComparison.OrdinalIgnoreCase))
           ?? throw new ArgumentException($"Option --framework requires one of: {string.Join(", ", All.Select(framework => framework.Name))}.");
}
