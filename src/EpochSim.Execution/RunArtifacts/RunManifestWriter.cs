using System.Text.Json;
using EpochSim.Execution;

namespace EpochSim.Execution.RunArtifacts;

public static class RunManifestWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    public static void Write(string path, RunManifest manifest)
    {
        var json = JsonSerializer.Serialize(manifest, Options);
        File.WriteAllText(path, json);
    }

    public static string GetEngineVersion()
        => typeof(SimulationEngine<>).Assembly.GetName().Version?.ToString() ?? "unknown";
}
