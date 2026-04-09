using System.Diagnostics;
using System.Text;

namespace MouseTool.Tests;

public sealed class UpdaterFlowTests
{
    [Fact]
    public async Task Updater_RunsUninstallerInstallerAndRelaunchesApp()
    {
        var repoRoot = TestPathHelper.FindRepositoryRoot();
        var tempRoot = Path.Combine(Path.GetTempPath(), "MouseTool-Updater-Test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var updaterPublishDir = Path.Combine(tempRoot, "published-updater");
            var stubPublishDir = Path.Combine(tempRoot, "published-stub");
            await PublishProjectAsync(Path.Combine(repoRoot, "Updater", "MouseTool.Updater.csproj"), updaterPublishDir);
            await PublishProjectAsync(Path.Combine(repoRoot, "Tests", "UpdaterStub", "UpdaterStub.csproj"), stubPublishDir);

            var updaterPath = Path.Combine(updaterPublishDir, "MouseTool.Updater.exe");
            var stubPath = Path.Combine(stubPublishDir, "UpdaterStub.exe");

            Assert.True(File.Exists(updaterPath), $"Updater executable not found: {updaterPath}");
            Assert.True(File.Exists(stubPath), $"Updater stub executable not found: {stubPath}");

            var appDirectory = Path.Combine(tempRoot, "app");
            var installerDirectory = Path.Combine(tempRoot, "installer");
            Directory.CreateDirectory(appDirectory);
            Directory.CreateDirectory(installerDirectory);

            var uninstallerPath = Path.Combine(appDirectory, "unins000.exe");
            var installerPath = Path.Combine(installerDirectory, "MouseTool-Setup.exe");
            var appPath = Path.Combine(appDirectory, "MouseTool.exe");
            var logPath = Path.Combine(tempRoot, "updater-flow.log");

            File.Copy(stubPath, uninstallerPath, overwrite: true);
            File.Copy(stubPath, installerPath, overwrite: true);
            File.Copy(stubPath, appPath, overwrite: true);

            using var blocker = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = "-NoProfile -Command Start-Sleep -Milliseconds 600",
                UseShellExecute = false,
                CreateNoWindow = true
            });

            Assert.NotNull(blocker);

            var updaterStartInfo = new ProcessStartInfo
            {
                FileName = updaterPath,
                Arguments = $"--pid {blocker!.Id} --installer \"{installerPath}\" --app-dir \"{appDirectory}\" --app-exe \"{appPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            updaterStartInfo.Environment["MOUSETOOL_UPDATER_STUB_LOG"] = logPath;

            using var updater = Process.Start(updaterStartInfo);

            Assert.NotNull(updater);
            await updater!.WaitForExitAsync();

            Assert.Equal(0, updater.ExitCode);
            await WaitForLogLineCountAsync(logPath, expectedCount: 3);
            Assert.True(File.Exists(logPath));

            var lines = (await File.ReadAllLinesAsync(logPath, Encoding.UTF8))
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToArray();

            Assert.Equal(["unins000", "MouseTool-Setup", "MouseTool"], lines);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static async Task PublishProjectAsync(string projectPath, string outputDirectory)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "C:\\Program Files\\dotnet\\dotnet.exe",
            Arguments = $"publish \"{projectPath}\" -c Debug -r win-x64 --self-contained false -p:PublishSingleFile=true -o \"{outputDirectory}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);
        var stdOut = await process!.StandardOutput.ReadToEndAsync();
        var stdErr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.True(process.ExitCode == 0, $"Publish failed for {projectPath}.{Environment.NewLine}{stdOut}{Environment.NewLine}{stdErr}");
    }

    private static async Task WaitForLogLineCountAsync(string logPath, int expectedCount)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            if (File.Exists(logPath))
            {
                var count = (await File.ReadAllLinesAsync(logPath, Encoding.UTF8))
                    .Count(line => !string.IsNullOrWhiteSpace(line));

                if (count >= expectedCount)
                {
                    return;
                }
            }

            await Task.Delay(100);
        }

        throw new Xunit.Sdk.XunitException($"Timed out waiting for {expectedCount} updater log entries.");
    }

    private static void TryDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (UnauthorizedAccessException) when (attempt < 9)
            {
                Thread.Sleep(200);
            }
            catch (IOException) when (attempt < 9)
            {
                Thread.Sleep(200);
            }
        }
    }
}
