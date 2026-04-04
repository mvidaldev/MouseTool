using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace MouseTool;

internal sealed class TrayIconHost : IDisposable
{
    private const int CallbackMessageId = 0x8001;
    private readonly HwndSource _source;
    private readonly ContextMenu _menu;
    private readonly MenuItem _openItem;
    private readonly MenuItem _startItem;
    private readonly MenuItem _stopItem;
    private readonly MenuItem _helpItem;
    private readonly MenuItem _exitItem;
    private readonly uint _iconId = 1;
    private bool _disposed;

    public TrayIconHost(Icon icon, Action onOpen, Action onStart, Action onStop, Action onHelp, Action onExit)
    {
        _source = new HwndSource(new HwndSourceParameters("MouseToolTrayHost")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0x800000,
            ParentWindow = new IntPtr(-3)
        });
        _source.AddHook(WndProc);

        _menu = new ContextMenu();
        _openItem = CreateMenuItem(onOpen);
        _startItem = CreateMenuItem(onStart);
        _stopItem = CreateMenuItem(onStop);
        _helpItem = CreateMenuItem(onHelp);
        _exitItem = CreateMenuItem(onExit);

        _menu.Items.Add(_openItem);
        _menu.Items.Add(_startItem);
        _menu.Items.Add(_stopItem);
        _menu.Items.Add(new Separator());
        _menu.Items.Add(_helpItem);
        _menu.Items.Add(new Separator());
        _menu.Items.Add(_exitItem);
        _menu.Closed += (_, _) => NativeMethods.PostMessage(_source.Handle, NativeMethods.WmNull, IntPtr.Zero, IntPtr.Zero);

        var data = CreateNotifyIconData(NativeMethods.NimAdd, icon, string.Empty, string.Empty);
        NativeMethods.ShellNotifyIcon(NativeMethods.NimAdd, ref data);
        var versionData = CreateNotifyIconData(NativeMethods.NimSetversion, icon, string.Empty, string.Empty);
        versionData.Anonymous.uVersion = NativeMethods.NotifyIconVersion4;
        NativeMethods.ShellNotifyIcon(NativeMethods.NimSetversion, ref versionData);
    }

    public void Update(string tooltip, string openText, string startText, string stopText, string helpText, string exitText, bool canStart, bool canStop)
    {
        _openItem.Header = openText;
        _startItem.Header = startText;
        _stopItem.Header = stopText;
        _helpItem.Header = helpText;
        _exitItem.Header = exitText;
        _startItem.IsEnabled = canStart;
        _stopItem.IsEnabled = canStop;

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

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        var data = CreateNotifyIconData(NativeMethods.NimDelete, null, string.Empty, string.Empty);
        NativeMethods.ShellNotifyIcon(NativeMethods.NimDelete, ref data);
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }

    private MenuItem CreateMenuItem(Action onClick)
    {
        var item = new MenuItem();
        item.Click += (_, _) => onClick();
        return item;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == CallbackMessageId)
        {
            switch ((int)lParam)
            {
                case NativeMethods.WmLbuttonup:
                case NativeMethods.WmLbuttondblclk:
                    _openItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                    handled = true;
                    break;
                case NativeMethods.WmRbuttonup:
                    OpenMenu();
                    handled = true;
                    break;
            }
        }

        return IntPtr.Zero;
    }

    private void OpenMenu()
    {
        NativeMethods.GetCursorPos(out var cursorPoint);
        NativeMethods.SetForegroundWindow(_source.Handle);
        _menu.Placement = System.Windows.Controls.Primitives.PlacementMode.AbsolutePoint;
        _menu.HorizontalOffset = cursorPoint.x;
        _menu.VerticalOffset = cursorPoint.y;
        _menu.IsOpen = true;
    }

    private NativeMethods.NotifyIconData CreateNotifyIconData(uint message, Icon? icon, string tooltip, string balloonText)
    {
        return new NativeMethods.NotifyIconData
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.NotifyIconData>(),
            hWnd = _source.Handle,
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
