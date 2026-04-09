namespace MouseTool;

internal sealed class UpdateCheckResult
{
    private UpdateCheckResult(
        bool isSuccessful,
        bool isUpdateAvailable,
        Version currentVersion,
        Version? latestVersion,
        UpdateManifest? manifest,
        string? errorMessage)
    {
        IsSuccessful = isSuccessful;
        IsUpdateAvailable = isUpdateAvailable;
        CurrentVersion = currentVersion;
        LatestVersion = latestVersion;
        Manifest = manifest;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccessful { get; }

    public bool IsUpdateAvailable { get; }

    public Version CurrentVersion { get; }

    public Version? LatestVersion { get; }

    public UpdateManifest? Manifest { get; }

    public string? ErrorMessage { get; }

    public static UpdateCheckResult UpToDate(Version currentVersion, Version latestVersion) =>
        new(true, false, currentVersion, latestVersion, null, null);

    public static UpdateCheckResult UpdateAvailable(Version currentVersion, Version latestVersion, UpdateManifest manifest) =>
        new(true, true, currentVersion, latestVersion, manifest, null);

    public static UpdateCheckResult Failed(Version currentVersion, string errorMessage) =>
        new(false, false, currentVersion, null, null, errorMessage);
}
