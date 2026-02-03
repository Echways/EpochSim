using System.Text.Json;

namespace EpochSim.Execution.RunArtifacts;

public static class RunManifestReader
{
    private static readonly JsonSerializerOptions Options = new()
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
            return JsonSerializer.Deserialize<RunManifest>(json, Options);
        }
        catch
        {
            return null;
        }
    }
}
