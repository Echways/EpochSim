using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.ExceptionServices;

namespace EpochSim.Fuzzing;

internal static class FuzzRunner
{
    private const int MaxArtifactSeeds = 10;

    public static FuzzRunResult<TState> Trace<TState>(FuzzSettings<TState> settings, ulong seed)
        => new FuzzSeedRunner<TState>(settings).Trace(seed);

    public static FuzzReport Run<TState>(FuzzSettings<TState> settings)
    {
        var stopwatch = Stopwatch.StartNew();
        var runner = new FuzzSeedRunner<TState>(settings);
        var failures = new ConcurrentBag<FuzzFailure>();
        var seedsRun = 0;
        var failureCount = 0;

        var options = new ParallelOptions { MaxDegreeOfParallelism = settings.Parallelism };
        var stored = settings.Store?.Load().Where(entry => entry.Name == settings.Name).ToArray() ?? [];
        var storedSeeds = stored.SelectMany(entry => entry.AllSeeds).ToHashSet();
        var passedStoredSeeds = new ConcurrentDictionary<ulong, byte>();
        var seeds = Partitioner.Create(StoredSeedsFirst(storedSeeds, settings.Seeds), EnumerablePartitionerOptions.NoBuffering);

        try
        {
            Parallel.ForEach(seeds, options, (seed, loop) =>
            {
                var failure = runner.Check(seed);
                var completed = Interlocked.Increment(ref seedsRun);

                if (failure is null && storedSeeds.Contains(seed))
                    passedStoredSeeds[seed] = 0;

                if (failure is not null)
                {
                    failures.Add(failure);
                    Interlocked.Increment(ref failureCount);

                    if (settings.StopAtFirstFailure)
                        loop.Stop();
                }

                settings.Progress?.Report(new FuzzProgress(completed, Volatile.Read(ref failureCount)));
            });
        }
        catch (AggregateException aggregate) when (aggregate.InnerExceptions.Count == 1)
        {
            ExceptionDispatchInfo.Capture(aggregate.InnerExceptions[0]).Throw();
        }

        var knownSignatures = stored.Where(entry => !entry.Fixed).Select(entry => entry.Signature).ToHashSet();
        var ordered = failures
            .Select(failure => failure with { IsKnown = knownSignatures.Contains(failure.Signature) })
            .OrderBy(failure => failure.Seed)
            .ToList();
        var newlyFixed = new List<ulong>();

        if (settings.Store is { } store)
        {
            var merge = new FailureStoreMerge<TState>(settings, TimeProvider.System);
            var passed = passedStoredSeeds.Keys.ToHashSet();
            store.Update(current => merge.Apply(current, ordered, passed, newlyFixed));
        }

        if (settings.ArtifactsRoot is not null)
        {
            for (var i = 0; i < ordered.Count && i < MaxArtifactSeeds; i++)
                ordered[i] = ordered[i] with { ArtifactsDir = runner.WriteArtifacts(ordered[i]) };
        }

        stopwatch.Stop();
        return new FuzzReport(seedsRun, stopwatch.Elapsed, ordered, newlyFixed, settings.Store is not null);
    }

    private static IEnumerable<ulong> StoredSeedsFirst(IReadOnlySet<ulong> storedSeeds, IEnumerable<ulong> seeds)
        => storedSeeds.Count == 0
            ? seeds
            : storedSeeds.Order().Concat(seeds.Where(seed => !storedSeeds.Contains(seed)));
}
