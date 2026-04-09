using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.IO;

namespace MouseTool;

internal sealed class UpdateService
{
    private const string DefaultManifestUrl = "https://github.com/mvidaldev/MouseTool/releases/latest/download/update.json";
    private static readonly HttpClient HttpClient = new();
    private readonly string _manifestUrl;

    public UpdateService(string? manifestUrl)
    {
        _manifestUrl = string.IsNullOrWhiteSpace(manifestUrl) ? DefaultManifestUrl : manifestUrl.Trim();
        CurrentVersion = ResolveCurrentVersion();
    }

    public Version CurrentVersion { get; }

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(_manifestUrl, UriKind.Absolute, out var manifestUri))
        {
            return UpdateCheckResult.Failed(CurrentVersion, $"Invalid update manifest URL: {_manifestUrl}");
        }

        try
        {
            using var response = await HttpClient.GetAsync(manifestUri, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var manifest = await JsonSerializer.DeserializeAsync<UpdateManifest>(stream, cancellationToken: cancellationToken);

            if (manifest is null)
            {
                return UpdateCheckResult.Failed(CurrentVersion, "Update manifest could not be parsed.");
            }

            if (string.IsNullOrWhiteSpace(manifest.Version) || string.IsNullOrWhiteSpace(manifest.InstallerUrl) || string.IsNullOrWhiteSpace(manifest.Sha256))
            {
                return UpdateCheckResult.Failed(CurrentVersion, "Update manifest is missing required fields.");
            }

            var latestVersion = manifest.ParseVersion();
            if (latestVersion > CurrentVersion)
            {
                return UpdateCheckResult.UpdateAvailable(CurrentVersion, latestVersion, manifest);
            }

            return UpdateCheckResult.UpToDate(CurrentVersion, latestVersion);
        }
        catch (Exception ex)
        {
            return UpdateCheckResult.Failed(CurrentVersion, ex.Message);
        }
    }

    public async Task<string> DownloadInstallerAsync(UpdateManifest manifest, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(manifest.InstallerUrl, UriKind.Absolute, out var installerUri))
        {
            throw new InvalidOperationException("The installer URL in the update manifest is invalid.");
        }

        var version = manifest.ParseVersion();
        var destinationPath = Path.Combine(AppPaths.UpdateDirectory, $"MouseTool-Setup-{version}.exe");
        var tempPath = destinationPath + ".download";

        if (File.Exists(tempPath))
        {
            File.Delete(tempPath);
        }

        using var response = await HttpClient.GetAsync(installerUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using (var sourceStream = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var destinationStream = File.Create(tempPath))
        {
            await sourceStream.CopyToAsync(destinationStream, cancellationToken);
        }

        var actualHash = await ComputeSha256Async(tempPath, cancellationToken);
        if (!string.Equals(actualHash, manifest.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(tempPath);
            throw new InvalidOperationException("The downloaded installer failed the integrity check.");
        }

        if (File.Exists(destinationPath))
        {
            File.Delete(destinationPath);
        }

        File.Move(tempPath, destinationPath);
        return destinationPath;
    }

    private static Version ResolveCurrentVersion()
    {
        var informationalVersion = typeof(UpdateService).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .Select(attribute => attribute.InformationalVersion)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            var cleanVersion = informationalVersion.Split('+', 2)[0];
            if (Version.TryParse(cleanVersion, out var parsedInformationalVersion))
            {
                return parsedInformationalVersion;
            }
        }

        return typeof(UpdateService).Assembly.GetName().Version ?? new Version(0, 0, 0);
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }
}
