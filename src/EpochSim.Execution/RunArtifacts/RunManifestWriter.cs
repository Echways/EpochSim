using System.Text.Json;
using EpochSim.Execution;

namespace EpochSim.Execution.RunArtifacts;

public static class RunManifestWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public static void Write(string path, RunManifest manifest)
    {
        var json = JsonSerializer.Serialize(manifest, SerializerOptions);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, path, overwrite: true);
    }

    public static string GetEngineVersion()
        => typeof(SimulationEngine<>).Assembly.GetName().Version?.ToString() ?? "unknown";
}
