using System.Diagnostics;
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
    private readonly SemaphoreSlim _updateLock = new(1, 1);
    private readonly UpdateService _updateService;
    private MouseKeeperConfig _config;
    private MouseKeeperEngine? _engine;
    private MainWindow? _mainWindow;
    private UpdateManifest? _availableUpdate;
    private string? _downloadedInstallerPath;
    private string? _availableVersionText;
    private string? _updateErrorMessage;
    private UpdateWorkflowState _updateWorkflowState;
    private bool _exitRequested;

    public MouseKeeperApplicationContext()
    {
        _configPath = AppPaths.ConfigPath;
        _logPath = AppPaths.LogPath;
        _helpDirectory = AppPaths.HelpDirectory;
        AppLocalizer.Initialize(AppPaths.LanguageDirectory);

        _config = MouseKeeperConfig.LoadOrCreate(_configPath);
        _updateService = new UpdateService(_config.UpdateManifestUrl);
        _updateWorkflowState = DetectInitialUpdateState();

        MouseKeeperLog.Initialize(_logPath);
        MouseKeeperLog.SetEnabled(_config.LoggingEnabled);
        HelpFileWriter.EnsureHelpFiles(_helpDirectory);
        EnsureMonitorDefaults();

        _trayIcon = new TrayIconHost(
            BrandAssets.AppIcon,
            ShowMainForm,
            () => StartProtection(),
            () => StopProtection(),
            ExecuteTrayUpdateAction,
            OpenHelpFile,
            ExitApplication);

        SystemEvents.DisplaySettingsChanged += OnSystemDisplaySettingsChanged;
        SystemEvents.PowerModeChanged += OnSystemPowerModeChanged;
        SystemEvents.SessionSwitch += OnSystemSessionSwitch;

        if (_config.Enabled)
        {
            StartProtection(showBalloon: false);
        }

        ShowMainForm();
        ApplyLanguageToThread();

        if (_config.CheckForUpdatesOnStartup && IsSelfUpdateSupported)
        {
            _ = CheckForUpdatesAsync(userInitiated: false);
        }
    }

    public bool IsRunning => _engine is not null;

    public bool LoggingEnabled => MouseKeeperLog.Enabled;

    public MouseKeeperConfig Config => _config;

    public string CurrentLanguageCode => AppLocalizer.ResolveLanguageCode(_config.SelectedLanguage);

    public string CurrentVersionText => FormatVersion(_updateService.CurrentVersion);

    public bool IsSelfUpdateSupported => File.Exists(AppPaths.InstalledUpdaterPath) && !string.IsNullOrWhiteSpace(AppPaths.FindUninstallerPath());

    public bool IsUpdateBusy => _updateWorkflowState is UpdateWorkflowState.Checking or UpdateWorkflowState.Downloading or UpdateWorkflowState.Applying;

    public bool CanCheckForUpdates => IsSelfUpdateSupported && !IsUpdateBusy;

    public bool CanInstallUpdate => IsSelfUpdateSupported && !IsUpdateBusy && _availableUpdate is not null;

    public bool CanOpenUpdateChangelog =>
        _availableUpdate is not null
        && (!string.IsNullOrWhiteSpace(_availableUpdate.ChangelogUrl) || !string.IsNullOrWhiteSpace(_availableUpdate.ReleaseNotesUrl));

    public string UpdateButtonText => CanInstallUpdate ? T("UpdatesButtonInstall") : T("UpdatesButtonCheck");

    public IReadOnlyList<LanguageOption> GetLanguageOptions() => AppLocalizer.GetLanguageOptions(_config.SelectedLanguage);

    public IReadOnlyList<DisplayOption> GetDisplayOptions()
    {
        return MonitorManager.GetAllMonitors()
            .Select((screen, index) => DisplayOption.FromMonitor(screen, index, GetLocalizedMonitorRole(screen, index)))
            .ToList();
    }

    public string T(string key) => AppLocalizer.Get(_config.SelectedLanguage, key);

    public string StatusText => IsRunning ? T("StatusRunning") : T("StatusPaused");

    public string UpdateStatusTitle => _updateWorkflowState switch
    {
        UpdateWorkflowState.Unsupported => T("UpdatesStatusUnsupportedTitle"),
        UpdateWorkflowState.Checking => T("UpdatesStatusCheckingTitle"),
        UpdateWorkflowState.UpToDate => T("UpdatesStatusCurrentTitle"),
        UpdateWorkflowState.Available => T("UpdatesStatusAvailableTitle"),
        UpdateWorkflowState.Downloading => T("UpdatesStatusDownloadingTitle"),
        UpdateWorkflowState.Applying => T("UpdatesStatusApplyingTitle"),
        UpdateWorkflowState.Error => T("UpdatesStatusErrorTitle"),
        _ => T("UpdatesStatusCurrentTitle")
    };

    public string UpdateStatusBody => _updateWorkflowState switch
    {
        UpdateWorkflowState.Unsupported => T("UpdatesStatusUnsupportedBody"),
        UpdateWorkflowState.Checking => string.Format(T("UpdatesStatusCheckingBody"), CurrentVersionText),
        UpdateWorkflowState.UpToDate => string.Format(T("UpdatesStatusCurrentBody"), _availableVersionText ?? CurrentVersionText),
        UpdateWorkflowState.Available => string.Format(T("UpdatesStatusAvailableBody"), _availableVersionText ?? CurrentVersionText),
        UpdateWorkflowState.Downloading => string.Format(T("UpdatesStatusDownloadingBody"), _availableVersionText ?? CurrentVersionText),
        UpdateWorkflowState.Applying => string.Format(T("UpdatesStatusApplyingBody"), _availableVersionText ?? CurrentVersionText),
        UpdateWorkflowState.Error => string.Format(T("UpdatesStatusErrorBody"), _updateErrorMessage ?? T("UpdatesGenericError")),
        _ => string.Format(T("UpdatesStatusCurrentBody"), CurrentVersionText)
    };

    public string UpdateCurrentVersionText => string.Format(T("UpdatesCurrentVersionValue"), CurrentVersionText);

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
        Process.Start("explorer.exe", AppPaths.DataDirectory);
    }

    public void OpenHelpFile()
    {
        HelpFileWriter.EnsureHelpFiles(_helpDirectory);
        var helpPath = HelpFileWriter.GetHelpFilePath(_helpDirectory, AppLocalizer.ResolveLanguageCode(_config.SelectedLanguage));
        Process.Start(new ProcessStartInfo
        {
            FileName = helpPath,
            UseShellExecute = true
        });
    }

    public void OpenCoffeeLink()
    {
        Process.Start(new ProcessStartInfo
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
            Process.Start("notepad.exe", _logPath);
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

    public async Task CheckForUpdatesAsync(bool userInitiated)
    {
        if (!IsSelfUpdateSupported)
        {
            _updateWorkflowState = UpdateWorkflowState.Unsupported;
            UpdateState();
            return;
        }

        await _updateLock.WaitAsync();
        try
        {
            _updateWorkflowState = UpdateWorkflowState.Checking;
            _updateErrorMessage = null;
            UpdateState();

            var result = await _updateService.CheckForUpdatesAsync();
            if (!result.IsSuccessful)
            {
                _availableUpdate = null;
                _downloadedInstallerPath = null;
                _availableVersionText = null;
                _updateErrorMessage = result.ErrorMessage;
                _updateWorkflowState = UpdateWorkflowState.Error;
                MouseKeeperLog.Write($"Update check failed: {result.ErrorMessage}");
                UpdateState();
                return;
            }

            _availableVersionText = FormatVersion(result.LatestVersion ?? result.CurrentVersion);
            if (result.IsUpdateAvailable)
            {
                _availableUpdate = result.Manifest;
                _updateWorkflowState = UpdateWorkflowState.Available;
                MouseKeeperLog.Write($"Update available. Current={CurrentVersionText}, Latest={_availableVersionText}");
                UpdateState();

                if (!userInitiated)
                {
                    ShowTrayBalloon(
                        T("UpdatesAvailableBalloonTitle"),
                        string.Format(T("UpdatesAvailableBalloonBody"), _availableVersionText ?? CurrentVersionText));
                }

                return;
            }

            _availableUpdate = null;
            _downloadedInstallerPath = null;
            _updateWorkflowState = UpdateWorkflowState.UpToDate;
            MouseKeeperLog.Write($"Application is up to date. Current={CurrentVersionText}");
            UpdateState();
        }
        finally
        {
            _updateLock.Release();
        }
    }

    public async Task InstallUpdateAsync()
    {
        if (!IsSelfUpdateSupported)
        {
            _updateWorkflowState = UpdateWorkflowState.Unsupported;
            UpdateState();
            return;
        }

        if (_availableUpdate is null)
        {
            await CheckForUpdatesAsync(userInitiated: true);
            if (_availableUpdate is null)
            {
                return;
            }
        }

        await _updateLock.WaitAsync();
        try
        {
            var installerPath = _downloadedInstallerPath;
            if (string.IsNullOrWhiteSpace(installerPath) || !File.Exists(installerPath))
            {
                _updateWorkflowState = UpdateWorkflowState.Downloading;
                _updateErrorMessage = null;
                UpdateState();

                installerPath = await _updateService.DownloadInstallerAsync(_availableUpdate);
                _downloadedInstallerPath = installerPath;
                MouseKeeperLog.Write($"Update installer downloaded to {installerPath}");
            }

            _updateWorkflowState = UpdateWorkflowState.Applying;
            UpdateState();
            LaunchUpdaterAndExit(installerPath);
        }
        catch (Exception ex)
        {
            _updateErrorMessage = ex.Message;
            _updateWorkflowState = UpdateWorkflowState.Error;
            MouseKeeperLog.Write($"Update install failed: {ex}");
            UpdateState();
        }
        finally
        {
            _updateLock.Release();
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

    public void OpenUpdateChangelog()
    {
        if (_availableUpdate is null)
        {
            return;
        }

        var targetUrl = !string.IsNullOrWhiteSpace(_availableUpdate.ChangelogUrl)
            ? _availableUpdate.ChangelogUrl
            : _availableUpdate.ReleaseNotesUrl;

        if (string.IsNullOrWhiteSpace(targetUrl))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = targetUrl,
            UseShellExecute = true
        });
    }

    private void ExecuteTrayUpdateAction()
    {
        if (CanInstallUpdate)
        {
            _ = InstallUpdateAsync();
            return;
        }

        _ = CheckForUpdatesAsync(userInitiated: true);
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
            var exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            key.SetValue(StartupValueName, $"\"{exePath}\"");
        }
        else
        {
            key.DeleteValue(StartupValueName, false);
        }
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
            CanInstallUpdate ? T("UpdatesButtonInstall") : T("MenuCheckUpdates"),
            T("MenuHelp"),
            T("MenuExit"),
            !IsRunning,
            IsRunning,
            CanCheckForUpdates || CanInstallUpdate);
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

    private void LaunchUpdaterAndExit(string installerPath)
    {
        if (!File.Exists(AppPaths.InstalledUpdaterPath))
        {
            throw new FileNotFoundException("The updater executable was not found.", AppPaths.InstalledUpdaterPath);
        }

        foreach (var staleUpdater in Directory.GetFiles(AppPaths.UpdateDirectory, "MouseTool.Updater.*.exe"))
        {
            try
            {
                File.Delete(staleUpdater);
            }
            catch
            {
                // Best-effort cleanup only.
            }
        }

        var updaterRuntimePath = Path.Combine(AppPaths.UpdateDirectory, $"MouseTool.Updater.{Guid.NewGuid():N}.exe");
        File.Copy(AppPaths.InstalledUpdaterPath, updaterRuntimePath, overwrite: true);

        var arguments = string.Join(" ",
            $"--pid {Process.GetCurrentProcess().Id}",
            $"--installer \"{installerPath}\"",
            $"--app-dir \"{AppPaths.InstallDirectory}\"",
            $"--app-exe \"{AppPaths.MainExecutablePath}\"");

        Process.Start(new ProcessStartInfo
        {
            FileName = updaterRuntimePath,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = AppPaths.UpdateDirectory
        });

        ShowTrayBalloon(T("UpdatesApplyingBalloonTitle"), string.Format(T("UpdatesApplyingBalloonBody"), _availableVersionText ?? CurrentVersionText));
        ExitApplication();
    }

    private UpdateWorkflowState DetectInitialUpdateState()
    {
        return IsSelfUpdateSupported ? UpdateWorkflowState.UpToDate : UpdateWorkflowState.Unsupported;
    }

    private static string FormatVersion(Version? version)
    {
        if (version is null)
        {
            return "0.0.0";
        }

        return version.Build >= 0
            ? $"{version.Major}.{version.Minor}.{version.Build}"
            : $"{version.Major}.{version.Minor}";
    }

    public void Dispose()
    {
        SystemEvents.DisplaySettingsChanged -= OnSystemDisplaySettingsChanged;
        SystemEvents.PowerModeChanged -= OnSystemPowerModeChanged;
        SystemEvents.SessionSwitch -= OnSystemSessionSwitch;
        _trayIcon.Dispose();
        _engine?.Dispose();
        _engine = null;
        _updateLock.Dispose();
    }

    private enum UpdateWorkflowState
    {
        Unsupported,
        Checking,
        UpToDate,
        Available,
        Downloading,
        Applying,
        Error
    }
}
