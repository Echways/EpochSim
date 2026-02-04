namespace EpochSim.Cli.Domain;

public static class DomainAdapterRegistry
{
    private static readonly Dictionary<string, IDomainAdapter> Adapters =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["population"] = new PopulationDomainAdapter()
        };

    public static IDomainAdapter Default => Adapters["population"];

    public static IDomainAdapter Resolve(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Default;

        if (Adapters.TryGetValue(name.Trim(), out var adapter))
            return adapter;

        throw new InvalidOperationException($"Unknown domain adapter: {name}");
    }

    public static IEnumerable<string> Names => Adapters.Keys;
}
