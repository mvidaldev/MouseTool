using System.Drawing;

namespace MouseTool;

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

        return MonitorManager.FromPoint(point) is { IsPrimary: false };
    }

    private MonitorInfo? GetConfiguredPrimaryMonitor()
    {
        return MonitorManager.FindByDeviceName(_config.PrimaryMonitorDeviceName)
            ?? MonitorManager.GetPrimaryMonitor();
    }

    private MonitorInfo? GetConfiguredTouchMonitor()
    {
        if (!string.IsNullOrWhiteSpace(_config.TouchMonitorDeviceName))
        {
            return MonitorManager.FindByDeviceName(_config.TouchMonitorDeviceName);
        }

        return MonitorManager.GetAllMonitors().FirstOrDefault(s => !s.IsPrimary);
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

