using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using System.Windows.Threading;

namespace MouseTool;

internal sealed class MouseKeeperApplicationContext : IDisposable
{
    private const string StartupValueName = "MouseTool";
    private readonly string _configPath;
    private readonly string _logPath;
    private readonly string _helpDirectory;
    private readonly TrayIconHost _trayIcon;
    private MouseKeeperConfig _config;
    private MouseKeeperEngine? _engine;
    private MainWindow? _mainWindow;
    private bool _exitRequested;

    public MouseKeeperApplicationContext()
    {
        _configPath = Path.Combine(AppContext.BaseDirectory, "mousekeeper.config.json");
        _logPath = Path.Combine(AppContext.BaseDirectory, "mousekeeper.log");
        _helpDirectory = Path.Combine(AppContext.BaseDirectory, "help");
        AppLocalizer.Initialize(Path.Combine(AppContext.BaseDirectory, "lang"));

        _config = MouseKeeperConfig.LoadOrCreate(_configPath);
        MouseKeeperLog.Initialize(_logPath);
        MouseKeeperLog.SetEnabled(_config.LoggingEnabled);
        HelpFileWriter.EnsureHelpFiles(_helpDirectory);
        EnsureMonitorDefaults();

        _trayIcon = new TrayIconHost(BrandAssets.AppIcon, ShowMainForm, () => StartProtection(), () => StopProtection(), OpenHelpFile, ExitApplication);
        SystemEvents.DisplaySettingsChanged += OnSystemDisplaySettingsChanged;
        SystemEvents.PowerModeChanged += OnSystemPowerModeChanged;
        SystemEvents.SessionSwitch += OnSystemSessionSwitch;

        if (_config.Enabled)
        {
            StartProtection(showBalloon: false);
        }

        ShowMainForm();
        ApplyLanguageToThread();
    }

    public bool IsRunning => _engine is not null;

    public bool LoggingEnabled => MouseKeeperLog.Enabled;

    public MouseKeeperConfig Config => _config;

    public string CurrentLanguageCode => AppLocalizer.ResolveLanguageCode(_config.SelectedLanguage);

    public IReadOnlyList<LanguageOption> GetLanguageOptions() => AppLocalizer.GetLanguageOptions(_config.SelectedLanguage);

    public IReadOnlyList<DisplayOption> GetDisplayOptions()
    {
        return MonitorManager.GetAllMonitors()
            .Select((screen, index) => DisplayOption.FromMonitor(screen, index, GetLocalizedMonitorRole(screen, index)))
            .ToList();
    }

    public string T(string key) => AppLocalizer.Get(_config.SelectedLanguage, key);

    public string StatusText => IsRunning ? T("StatusRunning") : T("StatusPaused");

    public void StartProtection(bool showBalloon = true, bool persistEnabled = true)
    {
        if (_engine is not null)
        {
            MouseKeeperLog.Write("StartProtection ignored because engine is already running.");
            return;
        }

        if (persistEnabled)
        {
            _config.Enabled = true;
            SaveConfig(_config);
        }

        _engine = new MouseKeeperEngine(_config, SaveConfig);
        _engine.Start();
        MouseKeeperLog.Write("Protection started.");
        UpdateState();

        if (showBalloon)
        {
            ShowTrayBalloon(T("TrayRunningTitle"), T("TrayRunningMessage"));
        }
    }

    public void StopProtection(bool showBalloon = true, bool persistEnabled = true)
    {
        if (_engine is null)
        {
            MouseKeeperLog.Write("StopProtection ignored because engine is not running.");
            if (persistEnabled)
            {
                _config.Enabled = false;
                SaveConfig(_config);
            }
            UpdateState();
            return;
        }

        _engine.Dispose();
        _engine = null;
        MouseKeeperLog.Write("Protection stopped.");
        if (persistEnabled)
        {
            _config.Enabled = false;
            SaveConfig(_config);
        }
        UpdateState();

        if (showBalloon)
        {
            ShowTrayBalloon(T("TrayPausedTitle"), T("TrayPausedMessage"));
        }
    }

    public void ReloadConfig()
    {
        _config = MouseKeeperConfig.LoadOrCreate(_configPath);
        MouseKeeperLog.SetEnabled(_config.LoggingEnabled);
        EnsureMonitorDefaults();
        ApplyLanguageToThread();

        if (_engine is not null)
        {
            _engine.Reload(_config);
        }

        UpdateState();
    }

    public void OpenConfigFolder()
    {
        var folder = Path.GetDirectoryName(_configPath) ?? AppContext.BaseDirectory;
        System.Diagnostics.Process.Start("explorer.exe", folder);
    }

    public void OpenHelpFile()
    {
        HelpFileWriter.EnsureHelpFiles(_helpDirectory);
        var helpPath = HelpFileWriter.GetHelpFilePath(_helpDirectory, AppLocalizer.ResolveLanguageCode(_config.SelectedLanguage));
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = helpPath,
            UseShellExecute = true
        });
    }

    public void OpenCoffeeLink()
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "https://buymeacoffee.com/mvidaldev",
            UseShellExecute = true
        });
    }

    public void EnableLogging()
    {
        MouseKeeperLog.SetEnabled(true);
        _config.LoggingEnabled = true;
        SaveConfig(_config);
        MouseKeeperLog.Write("Manual logging enabled from UI.");
        _mainWindow?.RefreshView();
    }

    public void DisableLogging()
    {
        MouseKeeperLog.Write("Manual logging disabled from UI.");
        MouseKeeperLog.SetEnabled(false);
        _config.LoggingEnabled = false;
        SaveConfig(_config);
        _mainWindow?.RefreshView();
    }

    public void OpenLogFile()
    {
        if (File.Exists(_logPath))
        {
            System.Diagnostics.Process.Start("notepad.exe", _logPath);
        }
    }

    public void SaveUserPreferenceChanges()
    {
        ApplyStartupPreference();
        SaveConfig(_config);

        if (_engine is not null)
        {
            _engine.Reload(_config);
        }

        UpdateState();
    }

    public void UpdateSelectedDisplays(string? primaryDeviceName, string? touchDeviceName)
    {
        if (!string.IsNullOrWhiteSpace(primaryDeviceName))
        {
            _config.PrimaryMonitorDeviceName = primaryDeviceName;
        }

        if (!string.IsNullOrWhiteSpace(touchDeviceName))
        {
            _config.TouchMonitorDeviceName = touchDeviceName;
        }

        SaveUserPreferenceChanges();
        MouseKeeperLog.Write($"Display selection updated. Primary={_config.PrimaryMonitorDeviceName}, Touch={_config.TouchMonitorDeviceName}");
    }

    public bool IsRegisteredForStartup()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false);
        var value = key?.GetValue(StartupValueName) as string;
        return !string.IsNullOrWhiteSpace(value);
    }

    private void ApplyStartupPreference()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
        if (key is null)
        {
            return;
        }

        if (_config.StartWithWindows)
        {
            var exePath = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            key.SetValue(StartupValueName, $"\"{exePath}\"");
        }
        else
        {
            key.DeleteValue(StartupValueName, false);
        }
    }

    public bool SetLanguageAndRequiresRestart(string? languageCode)
    {
        var normalized = languageCode ?? string.Empty;
        if (string.Equals(_config.SelectedLanguage, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        _config.SelectedLanguage = normalized;
        SaveConfig(_config);
        return true;
    }

    public void ShowRestartRequiredMessage()
    {
        System.Windows.MessageBox.Show(
            T("RestartRequiredMessage"),
            T("RestartRequiredTitle"),
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }

    public void ShowMainForm()
    {
        if (_mainWindow is null)
        {
            _mainWindow = new MainWindow(this);
            _mainWindow.Closed += (_, _) =>
            {
                if (!_exitRequested)
                {
                    _mainWindow = null;
                }
            };
        }

        if (!_mainWindow.IsVisible)
        {
            _mainWindow.Show();
        }

        if (_mainWindow.WindowState == System.Windows.WindowState.Minimized)
        {
            _mainWindow.WindowState = System.Windows.WindowState.Normal;
        }

        _mainWindow.Activate();
        _mainWindow.Topmost = true;
        _mainWindow.Topmost = false;
        _mainWindow.Focus();
        _mainWindow.RefreshView();
    }

    public void MinimizeMainFormToTray()
    {
        if (_mainWindow is null)
        {
            return;
        }

        _mainWindow.Hide();
        ShowTrayBalloon(T("TrayMinimizedTitle"), T("TrayMinimizedMessage"));
    }

    public void ExitApplication()
    {
        _exitRequested = true;
        MouseKeeperLog.Write("Exit requested.");
        _mainWindow?.AllowCloseWithoutPrompt();

        System.Windows.Application.Current?.Shutdown();
    }

    private void ApplyLanguageToThread()
    {
        var culture = AppLocalizer.ResolveCulture(_config.SelectedLanguage);
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
    }

    private void ApplyMenuTexts()
    {
        _trayIcon.Update(
            T("AppName"),
            T("MenuOpenDashboard"),
            T("MenuStartProtection"),
            T("MenuPauseProtection"),
            T("MenuHelp"),
            T("MenuExit"),
            !IsRunning,
            IsRunning);
    }

    private string GetLocalizedMonitorRole(MonitorInfo screen, int index)
    {
        if (screen.IsPrimary)
        {
            return T("MonitorRoleMain");
        }

        if (screen.Bounds.X < 0)
        {
            return T("MonitorRoleLeft");
        }

        if (screen.Bounds.X > 0)
        {
            return T("MonitorRoleRight");
        }

        if (screen.Bounds.Y < 0)
        {
            return T("MonitorRoleUpper");
        }

        return T("MonitorRoleSecondary");
    }

    private void EnsureMonitorDefaults()
    {
        var screens = MonitorManager.GetAllMonitors();
        var primary = screens.FirstOrDefault(s => s.IsPrimary) ?? screens.FirstOrDefault();
        var secondary = screens.FirstOrDefault(s => !s.IsPrimary);

        if (primary is not null && string.IsNullOrWhiteSpace(_config.PrimaryMonitorDeviceName))
        {
            _config.PrimaryMonitorDeviceName = primary.DeviceName;
        }

        if (secondary is not null && string.IsNullOrWhiteSpace(_config.TouchMonitorDeviceName))
        {
            _config.TouchMonitorDeviceName = secondary.DeviceName;
        }

        if (_config.LastPrimaryMousePosition.ToPoint() == Point.Empty && primary is not null)
        {
            var center = new Point(primary.Bounds.Left + primary.Bounds.Width / 2, primary.Bounds.Top + primary.Bounds.Height / 2);
            _config.LastPrimaryMousePosition = SerializablePoint.FromPoint(center);
        }

        SaveConfig(_config);
        MouseKeeperLog.Write($"Monitor defaults ensured. Primary={_config.PrimaryMonitorDeviceName}, Touch={_config.TouchMonitorDeviceName}, Enabled={_config.Enabled}");
    }

    private void SaveConfig(MouseKeeperConfig config)
    {
        var json = JsonSerializer.Serialize(config, MouseKeeperConfig.JsonOptions);
        File.WriteAllText(_configPath, json);
    }

    private void ShowTrayBalloon(string title, string message)
    {
        _trayIcon.ShowBalloon(title, message);
    }

    private void UpdateState()
    {
        ApplyMenuTexts();
        _mainWindow?.RefreshView();
    }

    private void OnSystemDisplaySettingsChanged(object? sender, EventArgs e)
    {
        HandleDisplayContextChanged("Display settings changed.");
    }

    private void OnSystemPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode is PowerModes.Resume or PowerModes.StatusChange)
        {
            HandleDisplayContextChanged($"Power mode changed: {e.Mode}.");
        }
    }

    private void OnSystemSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        if (e.Reason is SessionSwitchReason.SessionUnlock or SessionSwitchReason.ConsoleConnect or SessionSwitchReason.RemoteConnect)
        {
            HandleDisplayContextChanged($"Session switch detected: {e.Reason}.");
        }
    }

    private void HandleDisplayContextChanged(string reason)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            try
            {
                MouseKeeperLog.Write(reason);
                EnsureMonitorDefaults();
                _engine?.Reload(_config);
                _trayIcon.RestoreIconWithRetries();
                ApplyMenuTexts();
                _mainWindow?.RefreshView();
            }
            catch (Exception ex)
            {
                MouseKeeperLog.Write($"Display context refresh failed: {ex}");
            }
        }));
    }

    public void Dispose()
    {
        SystemEvents.DisplaySettingsChanged -= OnSystemDisplaySettingsChanged;
        SystemEvents.PowerModeChanged -= OnSystemPowerModeChanged;
        SystemEvents.SessionSwitch -= OnSystemSessionSwitch;
        _trayIcon.Dispose();
        _engine?.Dispose();
        _engine = null;
    }
}

