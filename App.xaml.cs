using System.Windows;

namespace MouseTool;

public partial class App : System.Windows.Application
{
    private MouseKeeperApplicationContext? _context;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        _context = new MouseKeeperApplicationContext();
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        _context?.Dispose();
        _context = null;
    }
}
