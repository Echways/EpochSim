using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EpochSim.Fuzzing;

public sealed class FuzzFailureStore
{
    public const string DefaultPath = "epochsim-failures.json";

    private const int SchemaVersion = 1;
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(30);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public FuzzFailureStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Path = System.IO.Path.GetFullPath(path);
    }

    public string Path { get; }

    public IReadOnlyList<FuzzStoredFailure> Load()
        => ReadFile(out _);

    public void Update(Func<IReadOnlyList<FuzzStoredFailure>, IReadOnlyList<FuzzStoredFailure>> change)
    {
        ArgumentNullException.ThrowIfNull(change);

        using var gate = AcquireGate();
        var current = ReadFile(out var currentText);
        var updated = change(current);
        var updatedText = Serialize(updated);

        if (string.Equals(currentText, updatedText, StringComparison.Ordinal) || (currentText is null && updated.Count == 0))
            return;

        WriteAtomically(updatedText);
    }

    private IReadOnlyList<FuzzStoredFailure> ReadFile(out string? text)
    {
        if (!File.Exists(Path))
        {
            text = null;
            return [];
        }

        text = File.ReadAllText(Path);

        try
        {
            var file = JsonSerializer.Deserialize<StoreFile>(text, JsonOptions)
                       ?? throw new JsonException("The file is empty.");

            if (file.Version > SchemaVersion)
                throw new InvalidDataException(
                    $"'{Path}' uses failure store schema version {file.Version}, but this EpochSim reads up to version {SchemaVersion}. Upgrade EpochSim.");

            return (file.Failures ?? []).Select(Normalize).ToArray();
        }
        catch (JsonException error)
        {
            throw new InvalidDataException($"'{Path}' is not a valid failure store: {error.Message}", error);
        }
    }

    private static FuzzStoredFailure Normalize(FuzzStoredFailure entry)
        => entry with
        {
            Name = entry.Name ?? string.Empty,
            StateType = entry.StateType ?? string.Empty,
            Identity = entry.Identity ?? string.Empty,
            Signature = entry.Signature ?? string.Empty,
            OtherSeeds = entry.OtherSeeds ?? []
        };

    private static string Serialize(IReadOnlyList<FuzzStoredFailure> entries)
    {
        var ordered = entries
            .OrderBy(entry => entry.Name, StringComparer.Ordinal)
            .ThenBy(entry => entry.Signature, StringComparer.Ordinal)
            .ThenBy(entry => entry.Seed)
            .ToArray();

        return JsonSerializer.Serialize(new StoreFile(SchemaVersion, ordered), JsonOptions) + "\n";
    }

    private void WriteAtomically(string text)
    {
        var directory = System.IO.Path.GetDirectoryName(Path)!;
        Directory.CreateDirectory(directory);

        var temporary = $"{Path}.{Guid.NewGuid():N}.tmp";

        try
        {
            File.WriteAllText(temporary, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temporary, Path, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private Gate AcquireGate()
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path)));
        var mutex = new Mutex(false, $"Global\\EpochSim.FailureStore.{hash}");

        try
        {
            if (!mutex.WaitOne(LockTimeout))
                throw new TimeoutException($"Timed out waiting for another process to finish writing '{Path}'.");
        }
        catch (AbandonedMutexException)
        {
        }
        catch
        {
            mutex.Dispose();
            throw;
        }

        return new Gate(mutex);
    }

    private sealed record StoreFile(int Version, IReadOnlyList<FuzzStoredFailure>? Failures);

    private readonly struct Gate(Mutex mutex) : IDisposable
    {
        public void Dispose()
        {
            mutex.ReleaseMutex();
            mutex.Dispose();
        }
    }
}
