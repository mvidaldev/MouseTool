using System.IO;

namespace MouseTool;

internal static class AppPaths
{
    private const string AppFolderName = "MouseTool";

    public static string InstallDirectory => Path.GetFullPath(AppContext.BaseDirectory);

    public static string DataDirectory => EnsureDirectory(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppFolderName));

    public static string ConfigPath => Path.Combine(DataDirectory, "mousekeeper.config.json");

    public static string LogPath => Path.Combine(DataDirectory, "mousekeeper.log");

    public static string HelpDirectory => EnsureDirectory(Path.Combine(DataDirectory, "help"));

    public static string UpdateDirectory => EnsureDirectory(Path.Combine(DataDirectory, "updates"));

    public static string LanguageDirectory => Path.Combine(InstallDirectory, "lang");

    public static string MainExecutablePath =>
        Environment.ProcessPath
        ?? Path.Combine(InstallDirectory, "MouseTool.exe");

    public static string InstalledUpdaterPath => Path.Combine(InstallDirectory, "MouseTool.Updater.exe");

    public static string? FindUninstallerPath()
    {
        if (!Directory.Exists(InstallDirectory))
        {
            return null;
        }

        return Directory.GetFiles(InstallDirectory, "unins*.exe")
            .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static string EnsureDirectory(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}
