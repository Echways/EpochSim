using System.Text.Json;

namespace EpochSim.Execution.RunArtifacts;

public static class RunManifestReader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static RunManifest? TryRead(string path)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<RunManifest>(json, SerializerOptions);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
