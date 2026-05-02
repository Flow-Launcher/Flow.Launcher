using System.Text.Json.Serialization;

namespace Flow.Launcher.Plugin.BrowserWorkspace.Models;

public class EdgeWorkspace
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("edgeWorkspaceVersion")]
    public int Version { get; set; }

    public string Profile { get; set; }

    public string EdgeExecutablePath { get; set; }
}
