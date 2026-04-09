using System.Text;

var executableName = Path.GetFileNameWithoutExtension(Environment.ProcessPath) ?? "unknown";
var logPath = Environment.GetEnvironmentVariable("MOUSETOOL_UPDATER_STUB_LOG");

if (!string.IsNullOrWhiteSpace(logPath))
{
    Directory.CreateDirectory(Path.GetDirectoryName(logPath) ?? Path.GetTempPath());
    File.AppendAllText(logPath, executableName + Environment.NewLine, Encoding.UTF8);
}

return 0;
