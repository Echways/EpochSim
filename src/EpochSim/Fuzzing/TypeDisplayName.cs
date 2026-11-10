namespace EpochSim.Fuzzing;

internal static class TypeDisplayName
{
    public static string Of(Type type)
    {
        if (type.IsArray)
            return Of(type.GetElementType()!) + "[]";

        var prefix = type.IsNested
            ? Of(type.DeclaringType!) + "."
            : string.IsNullOrEmpty(type.Namespace) ? string.Empty : type.Namespace + ".";

        if (!type.IsGenericType)
            return prefix + type.Name;

        var arguments = type.GetGenericArguments().Select(Of);
        return $"{prefix}{type.Name[..type.Name.IndexOf('`')]}<{string.Join(", ", arguments)}>";
    }
}
