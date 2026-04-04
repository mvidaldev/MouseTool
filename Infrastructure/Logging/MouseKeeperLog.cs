using System.IO;

namespace MouseTool;

internal static class MouseKeeperLog
{
    private static readonly Lock Sync = new();
    private static string _path = string.Empty;
    public static bool Enabled { get; private set; }

    public static void Initialize(string path)
    {
        _path = path;
        Write("Log initialized.");
    }

    public static void SetEnabled(bool enabled)
    {
        Enabled = enabled;
    }

    public static void Write(string message)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(_path))
        {
            return;
        }

        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} | {message}{Environment.NewLine}";
        lock (Sync)
        {
            File.AppendAllText(_path, line);
        }
    }
}



