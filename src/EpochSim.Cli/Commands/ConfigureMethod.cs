using System.Text.RegularExpressions;

namespace EpochSim.Cli.Commands;

internal sealed partial record ConfigureMethod(string Namespace, string TypeName, string MethodName)
{
    public string FullName
        => $"global::{(Namespace.Length > 0 ? Namespace + "." : string.Empty)}{TypeName}.{MethodName}";

    public static ConfigureMethod Parse(string? value)
    {
        var parts = (value ?? string.Empty).Split('.');

        if (parts.Length < 2 || !parts.All(part => Identifier().IsMatch(part)))
            throw new ArgumentException("Option --configure requires a static method as Namespace.Type.Method.");

        return new ConfigureMethod(string.Join('.', parts[..^2]), parts[^2], parts[^1]);
    }

    [GeneratedRegex(@"^[A-Za-z_]\w*$")]
    private static partial Regex Identifier();
}
