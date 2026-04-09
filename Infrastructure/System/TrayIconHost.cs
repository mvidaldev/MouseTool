using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;

namespace MouseTool;

internal sealed class TrayIconHost : IDisposable
{
    private const int CallbackMessageId = 0x8001;
    private const int WsPopup = unchecked((int)0x80000000);
    private const int WsExToolwindow = 0x00000080;
    private const int WsExNoactivate = 0x08000000;
    private static readonly uint TaskbarCreatedMessageId = NativeMethods.RegisterWindowMessage("TaskbarCreated");
    private readonly ContextMenu _menu;
    private readonly MenuItem _openItem;
    private readonly MenuItem _startItem;
    private readonly MenuItem _stopItem;
    private readonly MenuItem _updateItem;
    private readonly MenuItem _helpItem;
    private readonly MenuItem _exitItem;
    private readonly Icon _icon;
    private readonly Action _onOpen;
    private readonly Action _onStart;
    private readonly Action _onStop;
    private readonly Action _onUpdate;
    private readonly Action _onHelp;
    private readonly Action _onExit;
    private readonly uint _iconId = 1;
    private HwndSource? _source;
    private string _tooltip = string.Empty;
    private bool _disposed;

    public TrayIconHost(Icon icon, Action onOpen, Action onStart, Action onStop, Action onUpdate, Action onHelp, Action onExit)
    {
        _icon = (Icon)icon.Clone();
        _onOpen = onOpen;
        _onStart = onStart;
        _onStop = onStop;
        _onUpdate = onUpdate;
        _onHelp = onHelp;
        _onExit = onExit;

        _menu = new ContextMenu();
        _openItem = CreateMenuItem(_onOpen);
        _startItem = CreateMenuItem(_onStart);
        _stopItem = CreateMenuItem(_onStop);
        _updateItem = CreateMenuItem(_onUpdate);
        _helpItem = CreateMenuItem(_onHelp);
        _exitItem = CreateMenuItem(_onExit);

        _menu.Items.Add(_openItem);
        _menu.Items.Add(_startItem);
        _menu.Items.Add(_stopItem);
        _menu.Items.Add(_updateItem);
        _menu.Items.Add(new Separator());
        _menu.Items.Add(_helpItem);
        _menu.Items.Add(new Separator());
        _menu.Items.Add(_exitItem);
        _menu.Closed += (_, _) =>
        {
            var handle = _source?.Handle ?? IntPtr.Zero;
            if (handle != IntPtr.Zero)
            {
                NativeMethods.PostMessage(handle, NativeMethods.WmNull, IntPtr.Zero, IntPtr.Zero);
            }
        };

        CreateMessageWindow();
        AddNotifyIcon();
    }

    public void Update(
        string tooltip,
        string openText,
        string startText,
        string stopText,
        string updateText,
        string helpText,
        string exitText,
        bool canStart,
        bool canStop,
        bool canUpdate)
    {
        _tooltip = tooltip;
        _openItem.Header = openText;
        _startItem.Header = startText;
        _stopItem.Header = stopText;
        _updateItem.Header = updateText;
        _helpItem.Header = helpText;
        _exitItem.Header = exitText;
        _startItem.IsEnabled = canStart;
        _stopItem.IsEnabled = canStop;
        _updateItem.IsEnabled = canUpdate;

        var data = CreateNotifyIconData(NativeMethods.NimModify, null, tooltip, string.Empty);
        NativeMethods.ShellNotifyIcon(NativeMethods.NimModify, ref data);
    }

    public void ShowBalloon(string title, string message)
    {
        var data = CreateNotifyIconData(NativeMethods.NimModify, null, string.Empty, message);
        data.szInfoTitle = title;
        data.dwInfoFlags = NativeMethods.NiifInfo;
        NativeMethods.ShellNotifyIcon(NativeMethods.NimModify, ref data);
    }

    public void RestoreIcon()
    {
        if (_disposed)
        {
            return;
        }

        RecreateHost();
    }

    public void RestoreIconWithRetries()
    {
        if (_disposed)
        {
            return;
        }

        RestoreIcon();
        ScheduleRestoreAttempt(TimeSpan.FromMilliseconds(500));
        ScheduleRestoreAttempt(TimeSpan.FromSeconds(2));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        RemoveNotifyIcon();
        DestroyMessageWindow();
        _icon.Dispose();
    }

    private MenuItem CreateMenuItem(Action onClick)
    {
        var item = new MenuItem();
        item.Click += (_, _) => onClick();
        return item;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if ((uint)msg == TaskbarCreatedMessageId)
        {
            RestoreIconWithRetries();
            handled = true;
            return IntPtr.Zero;
        }

        if (msg == NativeMethods.WmDisplaychange)
        {
            RestoreIconWithRetries();
        }

        if (msg == NativeMethods.WmPowerbroadcast)
        {
            var powerEvent = wParam.ToInt32();
            if (powerEvent is NativeMethods.PbtApmresumeautomatic or NativeMethods.PbtApmresumesuspend or NativeMethods.PbtApmpowerstatuschange)
            {
                RestoreIconWithRetries();
            }
        }

        if (msg == CallbackMessageId)
        {
            var notification = lParam.ToInt32() & 0xFFFF;

            switch (notification)
            {
                case NativeMethods.NinSelect:
                case NativeMethods.NinKeyselect:
                case NativeMethods.WmLbuttonup:
                case NativeMethods.WmLbuttondblclk:
                    _openItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                    handled = true;
                    break;
                case NativeMethods.WmContextmenu:
                case NativeMethods.WmRbuttonup:
                    OpenMenu();
                    handled = true;
                    break;
            }
        }

        return IntPtr.Zero;
    }

    private void RecreateHost()
    {
        RemoveNotifyIcon();
        DestroyMessageWindow();
        CreateMessageWindow();
        AddNotifyIcon();
    }

    private void CreateMessageWindow()
    {
        var parameters = new HwndSourceParameters("MouseToolTrayHost")
        {
            Width = 0,
            Height = 0,
            WindowStyle = WsPopup,
            ExtendedWindowStyle = WsExToolwindow | WsExNoactivate
        };

        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    private void DestroyMessageWindow()
    {
        if (_source is null)
        {
            return;
        }

        _source.RemoveHook(WndProc);
        _source.Dispose();
        _source = null;
    }

    private void AddNotifyIcon()
    {
        var addData = CreateNotifyIconData(NativeMethods.NimAdd, _icon, _tooltip, string.Empty);
        NativeMethods.ShellNotifyIcon(NativeMethods.NimAdd, ref addData);

        var versionData = CreateNotifyIconData(NativeMethods.NimSetversion, _icon, _tooltip, string.Empty);
        versionData.Anonymous.uVersion = NativeMethods.NotifyIconVersion4;
        NativeMethods.ShellNotifyIcon(NativeMethods.NimSetversion, ref versionData);
    }

    private void RemoveNotifyIcon()
    {
        if (_source is null)
        {
            return;
        }

        var deleteData = CreateNotifyIconData(NativeMethods.NimDelete, null, string.Empty, string.Empty);
        NativeMethods.ShellNotifyIcon(NativeMethods.NimDelete, ref deleteData);
    }

    private void ScheduleRestoreAttempt(TimeSpan delay)
    {
        var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        var timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = delay
        };

        timer.Tick += (_, _) =>
        {
            timer.Stop();

            if (_disposed)
            {
                return;
            }

            RecreateHost();
        };

        timer.Start();
    }

    private void OpenMenu()
    {
        if (_source is null)
        {
            return;
        }

        NativeMethods.GetCursorPos(out var cursorPoint);
        NativeMethods.SetForegroundWindow(_source.Handle);
        _menu.Placement = System.Windows.Controls.Primitives.PlacementMode.AbsolutePoint;
        _menu.HorizontalOffset = cursorPoint.x;
        _menu.VerticalOffset = cursorPoint.y;
        _menu.IsOpen = true;
    }

    private NativeMethods.NotifyIconData CreateNotifyIconData(uint message, Icon? icon, string tooltip, string balloonText)
    {
        var handle = _source?.Handle ?? IntPtr.Zero;
        return new NativeMethods.NotifyIconData
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.NotifyIconData>(),
            hWnd = handle,
            uID = _iconId,
            uFlags = NativeMethods.NifMessage | NativeMethods.NifTip | NativeMethods.NifInfo | (icon is not null ? NativeMethods.NifIcon : 0),
            uCallbackMessage = CallbackMessageId,
            hIcon = icon?.Handle ?? IntPtr.Zero,
            szTip = tooltip,
            szInfo = balloonText,
            Anonymous = new NativeMethods.NotifyIconData.DummyUnion { uVersion = NativeMethods.NotifyIconVersion4 }
        };
    }
}
