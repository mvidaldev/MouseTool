using System.Drawing;
using System.Globalization;
using System.Text.Json;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MouseTool;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MouseKeeperApplicationContext());
    }
}

internal sealed class MouseKeeperApplicationContext : ApplicationContext
{
    private const string StartupValueName = "MouseTool";
    private readonly string _configPath;
    private readonly string _logPath;
    private readonly string _helpDirectory;
    private readonly NotifyIcon _notifyIcon;
    private MouseKeeperConfig _config;
    private MouseKeeperEngine? _engine;
    private MainForm? _mainForm;
    private ToolStripMenuItem? _startMenuItem;
    private ToolStripMenuItem? _stopMenuItem;
    private ToolStripMenuItem? _openDashboardMenuItem;
    private ToolStripMenuItem? _openHelpMenuItem;
    private ToolStripMenuItem? _exitMenuItem;
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

        _notifyIcon = new NotifyIcon
        {
            Text = AppLocalizer.Get(_config.SelectedLanguage, "AppName"),
            Visible = true,
            Icon = BrandAssets.AppIcon,
            ContextMenuStrip = BuildMenu()
        };
        _notifyIcon.DoubleClick += (_, _) => ShowMainForm();

        if (_config.Enabled)
        {
            StartProtection(showBalloon: false);
        }

        ShowMainForm();
        ApplyLanguageToThread();
        Application.ApplicationExit += OnApplicationExit;
    }

    public bool IsRunning => _engine is not null;

    public bool LoggingEnabled => MouseKeeperLog.Enabled;

    public MouseKeeperConfig Config => _config;

    public string CurrentLanguageCode => AppLocalizer.ResolveLanguageCode(_config.SelectedLanguage);

    public IReadOnlyList<LanguageOption> GetLanguageOptions() => AppLocalizer.GetLanguageOptions(_config.SelectedLanguage);

    public IReadOnlyList<DisplayOption> GetDisplayOptions()
    {
        return Screen.AllScreens
            .Select((screen, index) => DisplayOption.FromScreen(screen, index, GetLocalizedMonitorRole(screen, index)))
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
        _mainForm?.RefreshView();
    }

    public void DisableLogging()
    {
        MouseKeeperLog.Write("Manual logging disabled from UI.");
        MouseKeeperLog.SetEnabled(false);
        _config.LoggingEnabled = false;
        SaveConfig(_config);
        _mainForm?.RefreshView();
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
            var exePath = Application.ExecutablePath;
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
        MessageBox.Show(T("RestartRequiredMessage"), T("RestartRequiredTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    public void ShowMainForm()
    {
        if (_mainForm is null || _mainForm.IsDisposed)
        {
            _mainForm = new MainForm(this)
            {
                Icon = BrandAssets.AppIcon
            };
            _mainForm.FormClosed += (_, _) =>
            {
                if (!_exitRequested)
                {
                    _mainForm = null;
                }
            };
        }

        if (!_mainForm.Visible)
        {
            _mainForm.Show();
        }

        if (_mainForm.WindowState == FormWindowState.Minimized)
        {
            _mainForm.WindowState = FormWindowState.Normal;
        }

        _mainForm.BringToFront();
        _mainForm.Activate();
        _mainForm.RefreshView();
    }

    public void MinimizeMainFormToTray()
    {
        if (_mainForm is null || _mainForm.IsDisposed)
        {
            return;
        }

        _mainForm.Hide();
        ShowTrayBalloon(T("TrayMinimizedTitle"), T("TrayMinimizedMessage"));
    }

    public void ExitApplication()
    {
        _exitRequested = true;
        MouseKeeperLog.Write("Exit requested.");
        _mainForm?.AllowCloseWithoutPrompt();
        _mainForm?.Close();
        ExitThread();
    }

    private void ApplyLanguageToThread()
    {
        var culture = AppLocalizer.ResolveCulture(_config.SelectedLanguage);
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        _openDashboardMenuItem = new ToolStripMenuItem();
        _openDashboardMenuItem.Click += (_, _) => ShowMainForm();

        _startMenuItem = new ToolStripMenuItem();
        _startMenuItem.Click += (_, _) => StartProtection();

        _stopMenuItem = new ToolStripMenuItem();
        _stopMenuItem.Click += (_, _) => StopProtection();

        _openHelpMenuItem = new ToolStripMenuItem();
        _openHelpMenuItem.Click += (_, _) => OpenHelpFile();

        _exitMenuItem = new ToolStripMenuItem();
        _exitMenuItem.Click += (_, _) => ExitApplication();

        menu.Items.Add(_openDashboardMenuItem);
        menu.Items.Add(_startMenuItem);
        menu.Items.Add(_stopMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_openHelpMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_exitMenuItem);
        ApplyMenuTexts();
        return menu;
    }

    private void ApplyMenuTexts()
    {
        if (_notifyIcon is not null)
        {
            _notifyIcon.Text = T("AppName");
        }
        if (_openDashboardMenuItem is not null) _openDashboardMenuItem.Text = T("MenuOpenDashboard");
        if (_startMenuItem is not null) _startMenuItem.Text = T("MenuStartProtection");
        if (_stopMenuItem is not null) _stopMenuItem.Text = T("MenuPauseProtection");
        if (_openHelpMenuItem is not null) _openHelpMenuItem.Text = T("MenuHelp");
        if (_exitMenuItem is not null) _exitMenuItem.Text = T("MenuExit");
    }

    private string GetLocalizedMonitorRole(Screen screen, int index)
    {
        if (screen.Primary)
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
        var screens = Screen.AllScreens;
        var primary = screens.FirstOrDefault(s => s.Primary) ?? screens.FirstOrDefault();
        var secondary = screens.FirstOrDefault(s => !s.Primary);

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
        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = message;
        _notifyIcon.ShowBalloonTip(3000);
    }

    private void UpdateState()
    {
        if (_startMenuItem is not null)
        {
            _startMenuItem.Enabled = !IsRunning;
        }

        if (_stopMenuItem is not null)
        {
            _stopMenuItem.Enabled = IsRunning;
        }

        ApplyMenuTexts();
        _mainForm?.RefreshView();
    }

    private void OnApplicationExit(object? sender, EventArgs e)
    {
        _notifyIcon.Visible = false;
        _engine?.Dispose();
        _engine = null;
    }

    protected override void ExitThreadCore()
    {
        Application.ApplicationExit -= OnApplicationExit;
        _notifyIcon.Visible = false;
        _engine?.Dispose();
        _engine = null;
        base.ExitThreadCore();
    }
}

internal sealed class MouseKeeperEngine : IDisposable
{
    private readonly Action<MouseKeeperConfig> _saveConfig;
    private NativeMethods.LowLevelMouseProc? _hookProc;
    private IntPtr _hookHandle;
    private MouseKeeperConfig _config;
    private Point _lastPrimaryMousePosition;
    private bool _restorePending;
    private DateTime _lastSecondaryActivityUtc = DateTime.MinValue;
    private int _diagnosticEventsRemaining = 120;

    public MouseKeeperEngine(MouseKeeperConfig config, Action<MouseKeeperConfig> saveConfig)
    {
        _config = config;
        _saveConfig = saveConfig;
        _lastPrimaryMousePosition = config.LastPrimaryMousePosition.ToPoint();
    }

    public void Start()
    {
        _hookProc = MouseHookCallback;
        _hookHandle = NativeMethods.SetHook(_hookProc);
        MouseKeeperLog.Write($"Hook install result: 0x{_hookHandle.ToInt64():X}");
        RefreshMonitorDefaults();
    }

    public void Reload(MouseKeeperConfig config)
    {
        _config = config;
        _lastPrimaryMousePosition = config.LastPrimaryMousePosition.ToPoint();
        RefreshMonitorDefaults();
    }

    private void RefreshMonitorDefaults()
    {
        var screens = Screen.AllScreens;
        var primary = screens.FirstOrDefault(s => s.Primary) ?? screens.FirstOrDefault();
        var secondary = screens.FirstOrDefault(s => !s.Primary);

        if (primary is not null && string.IsNullOrWhiteSpace(_config.PrimaryMonitorDeviceName))
        {
            _config.PrimaryMonitorDeviceName = primary.DeviceName;
        }

        if (secondary is not null && string.IsNullOrWhiteSpace(_config.TouchMonitorDeviceName))
        {
            _config.TouchMonitorDeviceName = secondary.DeviceName;
        }

        if (_lastPrimaryMousePosition == Point.Empty && primary is not null)
        {
            var center = new Point(primary.Bounds.Left + primary.Bounds.Width / 2, primary.Bounds.Top + primary.Bounds.Height / 2);
            _lastPrimaryMousePosition = center;
            _config.LastPrimaryMousePosition = SerializablePoint.FromPoint(center);
        }

        _saveConfig(_config);
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0)
        {
            return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        var info = NativeMethods.PtrToMouseStruct(lParam);
        var point = new Point(info.pt.x, info.pt.y);
        var message = (NativeMethods.MouseMessage)wParam.ToInt32();
        var isTouchLike = NativeMethods.IsTouchOrPenInput(info.dwExtraInfo);
        LogDiagnosticEvent(point, message, info.dwExtraInfo, isTouchLike);

        if (isTouchLike)
        {
            HandleTouchInput(message, point);
        }
        else
        {
            var swallow = HandleRealMouseInput(message, point);
            if (swallow)
            {
                return (IntPtr)1;
            }
        }

        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private void LogDiagnosticEvent(Point point, NativeMethods.MouseMessage message, nuint extraInfo, bool isTouchLike)
    {
        if (_diagnosticEventsRemaining <= 0)
        {
            return;
        }

        var onPrimary = PointIsOnPrimaryMonitor(point);
        var onTouch = PointIsOnTouchMonitor(point);
        var interesting =
            onTouch ||
            isTouchLike ||
            extraInfo != 0 ||
            (message != NativeMethods.MouseMessage.WM_MOUSEMOVE && onPrimary);

        if (!interesting)
        {
            return;
        }

        _diagnosticEventsRemaining--;
        MouseKeeperLog.Write($"Raw event: msg={message}, point={point.X},{point.Y}, extra=0x{extraInfo.ToUInt64():X}, isTouchLike={isTouchLike}, onPrimary={onPrimary}, onTouch={onTouch}, restorePending={_restorePending}");
    }

    private void HandleTouchInput(NativeMethods.MouseMessage message, Point point)
    {
        if (!_config.Enabled || !PointIsOnTouchMonitor(point))
        {
            return;
        }

        if (!_restorePending)
        {
            _lastSecondaryActivityUtc = DateTime.UtcNow;
        }
        MouseKeeperLog.Write($"Touch event: {message} at {point.X},{point.Y}");

        switch (message)
        {
            case NativeMethods.MouseMessage.WM_MOUSEMOVE:
            case NativeMethods.MouseMessage.WM_LBUTTONUP:
            case NativeMethods.MouseMessage.WM_RBUTTONUP:
            case NativeMethods.MouseMessage.WM_MBUTTONUP:
                _restorePending = true;
                MouseKeeperLog.Write("Touch-like activity detected. Restore pending set.");
                if (_config.RestoreImmediatelyOnTouchRelease)
                {
                    RestoreMouseToPrimaryAnchor();
                }
                break;
        }
    }

    private bool HandleRealMouseInput(NativeMethods.MouseMessage message, Point point)
    {
        if (!_config.Enabled)
        {
            return false;
        }

        if (_restorePending &&
            message == NativeMethods.MouseMessage.WM_MOUSEMOVE &&
            (!PointIsOnTouchMonitor(point) || DateTime.UtcNow - _lastSecondaryActivityUtc > TimeSpan.FromMilliseconds(35)))
        {
            _restorePending = false;
            MouseKeeperLog.Write($"Mouse move after touch detected at {point.X},{point.Y}. Restoring anchor.");
            RestoreMouseToPrimaryAnchor();
            return true;
        }

        if (PointIsOnTouchMonitor(point))
        {
            if (_config.AllowMouseOnTouchscreen)
            {
                _restorePending = false;
                return false;
            }

            if (!_restorePending)
            {
                _restorePending = true;
                _lastSecondaryActivityUtc = DateTime.UtcNow;
            }
            return false;
        }

        if (PointIsOnPrimaryMonitor(point))
        {
            _lastPrimaryMousePosition = point;
            _config.LastPrimaryMousePosition = SerializablePoint.FromPoint(point);
            _saveConfig(_config);
        }

        return false;
    }

    private void RestoreMouseToPrimaryAnchor()
    {
        var primary = GetConfiguredPrimaryMonitor();
        if (primary is null)
        {
            MouseKeeperLog.Write("Restore skipped because primary monitor was not resolved.");
            return;
        }

        var bounded = ClampPointToBounds(_lastPrimaryMousePosition, primary.Bounds);
        _lastPrimaryMousePosition = bounded;
        _config.LastPrimaryMousePosition = SerializablePoint.FromPoint(bounded);
        _saveConfig(_config);
        MouseKeeperLog.Write($"Cursor restored to {bounded.X},{bounded.Y} on {primary.DeviceName}.");
        NativeMethods.SetCursorPos(bounded.X, bounded.Y);
    }

    private bool PointIsOnPrimaryMonitor(Point point)
    {
        var primary = GetConfiguredPrimaryMonitor();
        return primary is not null && primary.Bounds.Contains(point);
    }

    private bool PointIsOnTouchMonitor(Point point)
    {
        var touchScreen = GetConfiguredTouchMonitor();
        if (touchScreen is not null && touchScreen.Bounds.Contains(point))
        {
            return true;
        }

        return Screen.FromPoint(point) is { Primary: false };
    }

    private Screen? GetConfiguredPrimaryMonitor()
    {
        return Screen.AllScreens.FirstOrDefault(s => string.Equals(s.DeviceName, _config.PrimaryMonitorDeviceName, StringComparison.OrdinalIgnoreCase))
            ?? Screen.AllScreens.FirstOrDefault(s => s.Primary);
    }

    private Screen? GetConfiguredTouchMonitor()
    {
        if (!string.IsNullOrWhiteSpace(_config.TouchMonitorDeviceName))
        {
            return Screen.AllScreens.FirstOrDefault(s => string.Equals(s.DeviceName, _config.TouchMonitorDeviceName, StringComparison.OrdinalIgnoreCase));
        }

        return Screen.AllScreens.FirstOrDefault(s => !s.Primary);
    }

    private static Point ClampPointToBounds(Point point, Rectangle bounds)
    {
        var x = Math.Min(Math.Max(point.X, bounds.Left), bounds.Right - 1);
        var y = Math.Min(Math.Max(point.Y, bounds.Top), bounds.Bottom - 1);
        return new Point(x, y);
    }

    public void Dispose()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookHandle);
            MouseKeeperLog.Write("Hook removed.");
            _hookHandle = IntPtr.Zero;
        }
    }
}

internal sealed class MouseKeeperConfig
{
    public bool Enabled { get; set; } = true;
    public bool LoggingEnabled { get; set; }
    public bool StartWithWindows { get; set; }
    public string SelectedLanguage { get; set; } = string.Empty;
    public bool AllowMouseOnTouchscreen { get; set; }
    public string PrimaryMonitorDeviceName { get; set; } = string.Empty;
    public string TouchMonitorDeviceName { get; set; } = string.Empty;
    public bool RestoreImmediatelyOnTouchRelease { get; set; }
    public SerializablePoint LastPrimaryMousePosition { get; set; } = new();

    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        WriteIndented = true
    };

    public static MouseKeeperConfig LoadOrCreate(string path)
    {
        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);
            var loaded = JsonSerializer.Deserialize<MouseKeeperConfig>(json, JsonOptions);
            if (loaded is not null)
            {
                return loaded;
            }
        }

        var config = new MouseKeeperConfig
        {
            LoggingEnabled = false,
            SelectedLanguage = string.Empty
        };
        File.WriteAllText(path, JsonSerializer.Serialize(config, JsonOptions));
        return config;
    }
}

internal sealed class SerializablePoint
{
    public int X { get; set; }
    public int Y { get; set; }

    public Point ToPoint() => new(X, Y);

    public static SerializablePoint FromPoint(Point point) => new()
    {
        X = point.X,
        Y = point.Y
    };
}

internal sealed class DisplayOption
{
    public required string DeviceName { get; init; }
    public required string DisplayName { get; init; }

    public static DisplayOption FromScreen(Screen screen, int index, string role)
    {
        var bounds = screen.Bounds;
        var ordinal = index + 1;
        var detail = $"{bounds.Width}x{bounds.Height} at {bounds.X},{bounds.Y}";
        return new DisplayOption
        {
            DeviceName = screen.DeviceName,
            DisplayName = $"{role} {ordinal} ({detail})"
        };
    }
}

internal sealed class LanguageOption
{
    public required string Code { get; init; }
    public required string DisplayName { get; init; }
}

internal static class AppLocalizer
{
    private static readonly Dictionary<string, Dictionary<string, string>> Resources = new(StringComparer.OrdinalIgnoreCase);
    private static readonly (string Code, string Key)[] SupportedLanguages =
    [
        (string.Empty, "LanguageSystem"),
        ("en", "LanguageEnglish"),
        ("pt-BR", "LanguagePortugueseBrazil"),
        ("fr", "LanguageFrench"),
        ("es", "LanguageSpanish"),
        ("de", "LanguageGerman"),
        ("it", "LanguageItalian"),
        ("ru", "LanguageRussian")
    ];

    public static void Initialize(string directory)
    {
        Resources.Clear();

        foreach (var path in Directory.GetFiles(directory, "lang.*.json"))
        {
            var fileName = Path.GetFileNameWithoutExtension(path);
            var languageCode = fileName["lang.".Length..];
            LoadLanguageFile(directory, languageCode);
        }
    }

    private static void LoadLanguageFile(string directory, string languageCode)
    {
        var path = Path.Combine(directory, $"lang.{languageCode}.json");
        if (!File.Exists(path))
        {
            return;
        }

        var json = File.ReadAllText(path);
        var data = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
        Resources[languageCode] = new Dictionary<string, string>(data, StringComparer.OrdinalIgnoreCase);
    }

    public static string ResolveLanguageCode(string? preferredCode)
    {
        if (!string.IsNullOrWhiteSpace(preferredCode) && Resources.ContainsKey(preferredCode))
        {
            return preferredCode;
        }

        var system = CultureInfo.InstalledUICulture.Name;
        if (Resources.ContainsKey(system))
        {
            return system;
        }

        var neutral = CultureInfo.InstalledUICulture.TwoLetterISOLanguageName;
        if (!string.IsNullOrWhiteSpace(neutral) && Resources.ContainsKey(neutral))
        {
            return neutral;
        }

        if (system.StartsWith("pt", StringComparison.OrdinalIgnoreCase) && Resources.ContainsKey("pt-BR"))
        {
            return "pt-BR";
        }

        return Resources.ContainsKey("en") ? "en" : Resources.Keys.FirstOrDefault() ?? "en";
    }

    public static CultureInfo ResolveCulture(string? preferredCode) => new(ResolveLanguageCode(preferredCode));

    public static string Get(string? preferredCode, string key)
    {
        var code = ResolveLanguageCode(preferredCode);
        if (Resources.TryGetValue(code, out var resource) && resource.TryGetValue(key, out var value))
        {
            return value;
        }

        if (Resources.TryGetValue("en", out var fallback) && fallback.TryGetValue(key, out var english))
        {
            return english;
        }

        return key;
    }

    public static IReadOnlyList<LanguageOption> GetLanguageOptions(string? preferredCode)
    {
        return SupportedLanguages
            .Where(language => string.IsNullOrWhiteSpace(language.Code) || Resources.ContainsKey(language.Code))
            .Select(language => new LanguageOption
            {
                Code = language.Code,
                DisplayName = Get(preferredCode, language.Key)
            })
            .ToList();
    }
}
internal static class BrandAssets
{
    private static readonly Bitmap LogoBitmapValue = CreateLogoBitmap();
    private static readonly Icon AppIconValue = CreateAppIcon();
    private static readonly Bitmap HelpButtonIconValue = CreateHelpButtonIcon();
    private static readonly Bitmap CoffeeButtonIconValue = CreateCoffeeButtonIcon();

    public static Image LogoImage => LogoBitmapValue;
    public static Icon AppIcon => AppIconValue;
    public static Image HelpButtonIcon => HelpButtonIconValue;
    public static Image CoffeeButtonIcon => CoffeeButtonIconValue;

    private static Bitmap CreateLogoBitmap()
    {
        var bitmap = new Bitmap(88, 88);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        using var backgroundBrush = new SolidBrush(Color.FromArgb(18, 43, 74));
        using var accentBrush = new SolidBrush(Color.FromArgb(31, 132, 214));
        using var whiteBrush = new SolidBrush(Color.White);
        using var outlinePen = new Pen(Color.FromArgb(31, 132, 214), 4F);

        graphics.FillEllipse(backgroundBrush, 6, 6, 76, 76);
        graphics.FillEllipse(accentBrush, 16, 16, 56, 56);
        graphics.FillEllipse(whiteBrush, 27, 27, 34, 34);
        graphics.DrawEllipse(outlinePen, 18, 18, 52, 52);
        graphics.DrawLine(outlinePen, 44, 8, 44, 24);
        graphics.DrawLine(outlinePen, 44, 64, 44, 80);
        graphics.DrawLine(outlinePen, 8, 44, 24, 44);
        graphics.DrawLine(outlinePen, 64, 44, 80, 44);

        return bitmap;
    }

    private static Icon CreateAppIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.DrawImage(LogoBitmapValue, new Rectangle(0, 0, 32, 32));
        return Icon.FromHandle(bitmap.GetHicon());
    }

    private static Bitmap CreateHelpButtonIcon()
    {
        var bitmap = new Bitmap(18, 18);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        using var pen = new Pen(Color.FromArgb(21, 51, 91), 1.8F);
        using var brush = new SolidBrush(Color.FromArgb(21, 51, 91));
        using var font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold, GraphicsUnit.Pixel);

        graphics.DrawEllipse(pen, 1.5F, 1.5F, 15F, 15F);
        graphics.DrawString("?", font, brush, new RectangleF(3F, 1.5F, 12F, 12F));
        graphics.FillEllipse(brush, 8F, 13.5F, 2.5F, 2.5F);
        return bitmap;
    }

    private static Bitmap CreateCoffeeButtonIcon()
    {
        var bitmap = new Bitmap(18, 18);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        using var pen = new Pen(Color.FromArgb(106, 63, 0), 1.8F);

        graphics.DrawArc(pen, 10F, 5.5F, 5F, 6F, -60, 180);
        graphics.DrawLine(pen, 4F, 6F, 10F, 6F);
        graphics.DrawLine(pen, 4F, 6F, 4F, 11F);
        graphics.DrawLine(pen, 4F, 11F, 12F, 11F);
        graphics.DrawLine(pen, 12F, 11F, 12F, 6F);
        graphics.DrawLine(pen, 3F, 13.5F, 13.5F, 13.5F);
        graphics.DrawArc(pen, 6F, 2.5F, 2F, 4F, 180, 180);
        graphics.DrawArc(pen, 9F, 2.5F, 2F, 4F, 180, 180);
        return bitmap;
    }
}

internal static class HelpFileWriter
{
    public static void EnsureHelpFiles(string directoryPath)
    {
        Directory.CreateDirectory(directoryPath);

        var englishPath = GetHelpFilePath(directoryPath, "en");
        var portuguesePath = GetHelpFilePath(directoryPath, "pt-BR");

        if (!File.Exists(englishPath))
        {
            File.WriteAllText(englishPath, """
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <title>MouseTool Help</title>
  <style>
    body { font-family: Segoe UI, sans-serif; margin: 40px; color: #243041; background: #f5f7fa; }
    .card { background: white; border-radius: 16px; padding: 28px; margin-bottom: 24px; box-shadow: 0 8px 28px rgba(14, 32, 56, 0.08); }
    h1, h2 { color: #12304d; }
    p, li { line-height: 1.6; }
  </style>
</head>
<body>
  <div class="card">
    <h1>MouseTool Help</h1>
    <p>MouseTool keeps your mouse anchored to the main display while a touchscreen works on another monitor.</p>
  </div>
  <div class="card">
    <h2>How to Use</h2>
    <ol>
      <li>Select the correct primary display and touchscreen display in the Displays tab.</li>
      <li>Click Apply Changes.</li>
      <li>Use the physical mouse on the main display.</li>
      <li>Use touch on the touchscreen display.</li>
      <li>Move the physical mouse again to return quickly to the last saved position on the main display.</li>
    </ol>
  </div>
  <div class="card">
    <h2>Language and Diagnostics</h2>
    <p>The app defaults to the system language, but you can change the interface language from the top selector.</p>
    <p>Use the Diagnostics tab only when you need logs for testing or troubleshooting.</p>
  </div>
  <div class="card">
    <h2>Touchscreen Mouse Access</h2>
    <p>In the Behavior tab, you can allow or block the physical mouse from entering the monitor configured as the touchscreen area.</p>
  </div>
</body>
</html>
""");
        }

        if (!File.Exists(portuguesePath))
        {
            File.WriteAllText(portuguesePath, """
<!doctype html>
<html lang="pt-BR">
<head>
  <meta charset="utf-8">
  <title>Ajuda do MouseTool</title>
  <style>
    body { font-family: Segoe UI, sans-serif; margin: 40px; color: #243041; background: #f5f7fa; }
    .card { background: white; border-radius: 16px; padding: 28px; margin-bottom: 24px; box-shadow: 0 8px 28px rgba(14, 32, 56, 0.08); }
    h1, h2 { color: #12304d; }
    p, li { line-height: 1.6; }
  </style>
</head>
<body>
  <div class="card">
    <h1>Ajuda do MouseTool</h1>
    <p>O MouseTool mantem o mouse ancorado na tela principal enquanto a tela touchscreen funciona em outro monitor.</p>
  </div>
  <div class="card">
    <h2>Como usar</h2>
    <ol>
      <li>Selecione corretamente a tela principal e a tela touchscreen na aba Telas.</li>
      <li>Clique em Aplicar Alteracoes.</li>
      <li>Use o mouse fisico na tela principal.</li>
      <li>Use o toque na tela touchscreen.</li>
      <li>Mova o mouse fisico novamente para voltar rapidamente para a ultima posicao salva na tela principal.</li>
    </ol>
  </div>
  <div class="card">
    <h2>Idioma e diagnostico</h2>
    <p>O aplicativo usa por padrao o idioma do sistema, mas voce pode alterar o idioma da interface pelo seletor no topo da janela.</p>
    <p>Use a aba Diagnostico somente quando precisar de logs para testes ou suporte.</p>
  </div>
  <div class="card">
    <h2>Acesso do mouse a tela touchscreen</h2>
    <p>Na aba Comportamento, voce pode permitir ou bloquear que o mouse fisico entre no monitor configurado como area touchscreen.</p>
  </div>
</body>
</html>
""");
        }
    }

    public static string GetHelpFilePath(string directoryPath, string languageCode)
    {
        var normalized = string.Equals(languageCode, "pt-BR", StringComparison.OrdinalIgnoreCase) ? "pt-BR" : "en";
        return Path.Combine(directoryPath, $"HELP.{normalized}.html");
    }
}

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



