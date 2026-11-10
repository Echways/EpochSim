namespace EpochSim.Fuzzing;

internal sealed class FailureStoreMerge<TState>(FuzzSettings<TState> settings, TimeProvider timeProvider)
{
    public IReadOnlyList<FuzzStoredFailure> Apply(
        IReadOnlyList<FuzzStoredFailure> current,
        IReadOnlyList<FuzzFailure> failures,
        IReadOnlySet<ulong> passedStoredSeeds,
        List<ulong> newlyFixed)
    {
        var entries = current.ToList();

        foreach (var group in failures.GroupBy(failure => failure.Signature).OrderBy(group => group.Key, StringComparer.Ordinal))
            Record(entries, group.OrderBy(failure => failure.Seed).ToArray());

        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry.Name == settings.Name && !entry.Fixed && entry.AllSeeds.All(passedStoredSeeds.Contains))
            {
                entries[i] = entry with { Fixed = true };
                newlyFixed.Add(entry.Seed);
            }
        }

        return entries;
    }

    private void Record(List<FuzzStoredFailure> entries, IReadOnlyList<FuzzFailure> group)
    {
        var primary = group[0];
        var index = entries.FindIndex(entry => entry.Name == settings.Name && entry.Signature == primary.Signature);

        if (index < 0)
        {
            entries.Add(new FuzzStoredFailure(
                settings.Name,
                settings.StateType,
                primary.Seed,
                primary.Kind,
                primary.Identity,
                primary.Tick,
                primary.ReproTicks,
                primary.Signature,
                primary.MinimalCommandIndices ?? settings.OnlyCommands,
                group.Skip(1).Select(failure => failure.Seed).ToArray(),
                timeProvider.GetUtcNow(),
                Fixed: false));
            return;
        }

        var existing = entries[index];
        var otherSeeds = existing.OtherSeeds
            .Concat(group.Select(failure => failure.Seed))
            .Where(seed => seed != existing.Seed)
            .Distinct()
            .Order()
            .ToArray();

        entries[index] = existing with { OtherSeeds = otherSeeds, Fixed = false };
    }
}
