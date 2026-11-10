using System.Diagnostics;

namespace EpochSim.Fuzzing;

internal sealed record CommandShrinkResult(IReadOnlyList<int> Indices, int Attempts, bool BudgetExhausted);

internal static class CommandShrinker
{
    public static CommandShrinkResult Minimize(
        IReadOnlyList<int> indices,
        Func<IReadOnlyList<int>, bool> stillFails,
        FuzzShrinkBudget budget)
    {
        var stopwatch = Stopwatch.StartNew();
        var attempts = 0;
        var exhausted = false;

        bool Fails(IReadOnlyList<int> candidate)
        {
            if (attempts >= budget.MaxAttempts || stopwatch.Elapsed >= budget.TimeLimit)
            {
                exhausted = true;
                return false;
            }

            attempts++;
            return stillFails(candidate);
        }

        var current = indices;

        if (current.Count > 0 && Fails([]))
            return new CommandShrinkResult([], attempts, exhausted);

        var granularity = 2;

        while (current.Count >= 2 && !exhausted)
        {
            var chunks = Split(current, granularity);

            if (TryReduceToChunk(chunks, Fails, out var chunk))
            {
                current = chunk;
                granularity = 2;
            }
            else if (TryReduceToComplement(chunks, Fails, out var complement))
            {
                current = complement;
                granularity = Math.Max(granularity - 1, 2);
            }
            else if (granularity < current.Count)
            {
                granularity = Math.Min(granularity * 2, current.Count);
            }
            else
            {
                break;
            }
        }

        return new CommandShrinkResult(current, attempts, exhausted);
    }

    private static bool TryReduceToChunk(
        IReadOnlyList<IReadOnlyList<int>> chunks,
        Func<IReadOnlyList<int>, bool> fails,
        out IReadOnlyList<int> reduced)
    {
        foreach (var chunk in chunks)
        {
            if (fails(chunk))
            {
                reduced = chunk;
                return true;
            }
        }

        reduced = [];
        return false;
    }

    private static bool TryReduceToComplement(
        IReadOnlyList<IReadOnlyList<int>> chunks,
        Func<IReadOnlyList<int>, bool> fails,
        out IReadOnlyList<int> reduced)
    {
        for (var skipped = 0; skipped < chunks.Count; skipped++)
        {
            var complement = chunks.Where((_, position) => position != skipped).SelectMany(chunk => chunk).ToArray();
            if (fails(complement))
            {
                reduced = complement;
                return true;
            }
        }

        reduced = [];
        return false;
    }

    private static IReadOnlyList<IReadOnlyList<int>> Split(IReadOnlyList<int> items, int parts)
    {
        var chunks = new List<IReadOnlyList<int>>(parts);
        var start = 0;

        for (var part = 0; part < parts; part++)
        {
            var size = (items.Count - start) / (parts - part);
            chunks.Add(items.Skip(start).Take(size).ToArray());
            start += size;
        }

        return chunks;
    }
}
