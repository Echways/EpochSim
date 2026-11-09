using System.Text.Json;
using EpochSim.Fuzzing;

namespace EpochSim.Testing;

internal sealed record GoldenFile(ulong Seed, long Ticks, SortedDictionary<long, string> Fingerprints)
{
    private const long CheckpointInterval = 100;
    private const string UpdateVariable = "EPOCHSIM_UPDATE_GOLDEN";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static GoldenFile Capture<TState>(FuzzRunResult<TState> result, ulong seed, long ticks)
    {
        if (result.Error is { } error)
            throw new SimulationAssertionException($"The run failed before fingerprints could be compared: {error.GetType().Name}: {error.Message}");

        var checkpoints = result.Fingerprints
            .Where(record => record.Tick % CheckpointInterval == 0 || record.Tick == ticks)
            .ToDictionary(record => record.Tick, record => record.Hash);

        if (checkpoints.Count == 0)
            throw new SimulationAssertionException("A golden check needs a run of at least one tick.");

        return new GoldenFile(seed, ticks, new SortedDictionary<long, string>(checkpoints));
    }

    public static void Verify(string path, GoldenFile actual)
    {
        if (!File.Exists(path) || UpdateRequested())
        {
            Write(path, actual);
            return;
        }

        var expected = Read(path);

        if (expected.Seed != actual.Seed || expected.Ticks != actual.Ticks)
        {
            throw new SimulationAssertionException(
                $"Golden file '{path}' was recorded with seed {expected.Seed} and {expected.Ticks} ticks, " +
                $"but the check ran with seed {actual.Seed} and {actual.Ticks} ticks. {UpdateHint}");
        }

        var tick = expected.Fingerprints.Keys.Union(actual.Fingerprints.Keys).Order()
            .FirstOrDefault(candidate => !Same(expected, actual, candidate), -1L);

        if (tick >= 0)
        {
            throw new SimulationAssertionException(
                $"Golden fingerprint mismatch at tick {tick} (seed {actual.Seed}): " +
                $"expected {Describe(expected, tick)}, actual {Describe(actual, tick)}. {UpdateHint}");
        }
    }

    private static string UpdateHint => $"Set {UpdateVariable}=1 to accept the current behavior.";

    private static bool UpdateRequested()
        => string.Equals(Environment.GetEnvironmentVariable(UpdateVariable), "1", StringComparison.Ordinal);

    private static bool Same(GoldenFile expected, GoldenFile actual, long tick)
        => expected.Fingerprints.TryGetValue(tick, out var left)
           && actual.Fingerprints.TryGetValue(tick, out var right)
           && string.Equals(left, right, StringComparison.Ordinal);

    private static string Describe(GoldenFile file, long tick)
        => file.Fingerprints.TryGetValue(tick, out var hash) ? hash : "<missing>";

    private static GoldenFile Read(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<GoldenFile>(File.ReadAllText(path), JsonOptions)
                   ?? throw new JsonException("The file is empty.");
        }
        catch (JsonException ex)
        {
            throw new SimulationAssertionException($"Golden file '{path}' is not valid: {ex.Message} {UpdateHint}");
        }
    }

    private static void Write(string path, GoldenFile file)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(path, JsonSerializer.Serialize(file, JsonOptions));
    }
}
