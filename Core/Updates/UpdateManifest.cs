using System.Text.Json.Serialization;

namespace MouseTool;

internal sealed class UpdateManifest
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("installerUrl")]
    public string InstallerUrl { get; set; } = string.Empty;

    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = string.Empty;

    [JsonPropertyName("publishedAt")]
    public string? PublishedAt { get; set; }

    [JsonPropertyName("releaseNotesUrl")]
    public string? ReleaseNotesUrl { get; set; }

    [JsonPropertyName("changelogUrl")]
    public string? ChangelogUrl { get; set; }

    public Version ParseVersion() => System.Version.Parse(Version);
}
