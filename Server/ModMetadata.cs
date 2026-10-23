using SPTarkov.Server.Core.Models.Spt.Mod;

namespace SenzeBodycam.Server;

public sealed record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.senze.bodycam.server";
    public string Name { get; init; } = "Senze Bodycam";
    public string Author { get; init; } = "Senze";
    public List<string>? Contributors { get; init; }
    public SemanticVersioning.Version Version { get; init; } = new("1.0.0");
    public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1.0");
    public bool HasPrepatcher { get; init; }
    public List<string>? Incompatibilities { get; init; }
    public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public string? Url { get; init; } = "https://github.com/notSENZE/Bodycam";
    public string License { get; init; } = "MIT";
}
