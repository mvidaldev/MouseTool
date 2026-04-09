using System.Diagnostics;

var arguments = ParseArguments(args);

if (!arguments.TryGetValue("--pid", out var pidValue) || !int.TryParse(pidValue, out var targetPid))
{
    throw new InvalidOperationException("Missing or invalid --pid argument.");
}

if (!arguments.TryGetValue("--installer", out var installerPath) || string.IsNullOrWhiteSpace(installerPath))
{
    throw new InvalidOperationException("Missing --installer argument.");
}

if (!arguments.TryGetValue("--app-dir", out var appDirectory) || string.IsNullOrWhiteSpace(appDirectory))
{
    throw new InvalidOperationException("Missing --app-dir argument.");
}

if (!arguments.TryGetValue("--app-exe", out var appExecutablePath) || string.IsNullOrWhiteSpace(appExecutablePath))
{
    throw new InvalidOperationException("Missing --app-exe argument.");
}

WaitForProcessExit(targetPid, TimeSpan.FromSeconds(45));

var uninstallerPath = Directory.GetFiles(appDirectory, "unins*.exe")
    .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
    .FirstOrDefault();

if (string.IsNullOrWhiteSpace(uninstallerPath) || !File.Exists(uninstallerPath))
{
    throw new FileNotFoundException("The installed MouseTool uninstaller was not found.", uninstallerPath);
}

if (!File.Exists(installerPath))
{
    throw new FileNotFoundException("The downloaded MouseTool installer was not found.", installerPath);
}

RunProcess(uninstallerPath, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART", appDirectory);
RunProcess(installerPath, $"/SP- /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /DIR=\"{appDirectory}\"", Path.GetDirectoryName(installerPath) ?? appDirectory);

var installedExecutablePath = File.Exists(appExecutablePath)
    ? appExecutablePath
    : Path.Combine(appDirectory, "MouseTool.exe");

if (File.Exists(installedExecutablePath))
{
    Process.Start(new ProcessStartInfo
    {
        FileName = installedExecutablePath,
        WorkingDirectory = Path.GetDirectoryName(installedExecutablePath) ?? appDirectory,
        UseShellExecute = true
    });
}

static Dictionary<string, string> ParseArguments(string[] args)
{
    var parsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var index = 0; index < args.Length; index += 2)
    {
        var key = args[index];
        var value = index + 1 < args.Length ? args[index + 1] : string.Empty;
        parsed[key] = value;
    }

    return parsed;
}

static void WaitForProcessExit(int processId, TimeSpan timeout)
{
    try
    {
        using var process = Process.GetProcessById(processId);
        if (!process.WaitForExit((int)timeout.TotalMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
        }
    }
    catch (ArgumentException)
    {
        // The process already exited before the updater started waiting.
    }
}

static void RunProcess(string fileName, string arguments, string workingDirectory)
{
    using var process = Process.Start(new ProcessStartInfo
    {
        FileName = fileName,
        Arguments = arguments,
        WorkingDirectory = workingDirectory,
        UseShellExecute = false,
        CreateNoWindow = true
    });

    if (process is null)
    {
        throw new InvalidOperationException($"Could not start process: {fileName}");
    }

    process.WaitForExit();
    if (process.ExitCode != 0)
    {
        throw new InvalidOperationException($"Process failed with exit code {process.ExitCode}: {fileName}");
    }
}
