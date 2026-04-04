using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;

namespace MouseTool;

internal static class NativeMethods
{
    private const int WhMouseLl = 14;
    private const uint MiWpSignature = 0xFF515700;
    private const uint MiWpMask = 0xFFFFFF00;
    public const int MonitorDefaulttonearest = 2;
    public const int WmLbuttondblclk = 0x0203;
    public const int WmLbuttonup = 0x0202;
    public const int WmRbuttonup = 0x0205;
    public const int WmNull = 0x0000;
    public const uint MonitorInfofPrimary = 0x00000001;
    public const uint NimAdd = 0x00000000;
    public const uint NimModify = 0x00000001;
    public const uint NimDelete = 0x00000002;
    public const uint NimSetversion = 0x00000004;
    public const uint NifMessage = 0x00000001;
    public const uint NifIcon = 0x00000002;
    public const uint NifTip = 0x00000004;
    public const uint NifInfo = 0x00000010;
    public const uint NiifInfo = 0x00000001;
    public const uint NotifyIconVersion4 = 4;

    public delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
    public delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RectStruct lprcMonitor, IntPtr dwData);

    public enum MouseMessage
    {
        WM_MOUSEMOVE = 0x0200,
        WM_LBUTTONDOWN = 0x0201,
        WM_LBUTTONUP = 0x0202,
        WM_RBUTTONDOWN = 0x0204,
        WM_RBUTTONUP = 0x0205,
        WM_MBUTTONDOWN = 0x0207,
        WM_MBUTTONUP = 0x0208
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PointStruct
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RectStruct
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public Rectangle ToRectangle() => Rectangle.FromLTRB(Left, Top, Right, Bottom);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct MonitorInfoEx
    {
        public uint cbSize;
        public RectStruct rcMonitor;
        public RectStruct rcWork;
        public uint dwFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MsLlHookStruct
    {
        public PointStruct pt;
        public int mouseData;
        public int flags;
        public int time;
        public nuint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct NotifyIconData
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;

        public uint dwState;
        public uint dwStateMask;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;

        public DummyUnion Anonymous;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;

        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;

        [StructLayout(LayoutKind.Explicit)]
        public struct DummyUnion
        {
            [FieldOffset(0)]
            public uint uTimeout;

            [FieldOffset(0)]
            public uint uVersion;
        }
    }

    public static bool IsTouchOrPenInput(nuint extraInfo)
    {
        var value = (uint)(extraInfo & uint.MaxValue);
        return (value & MiWpMask) == MiWpSignature;
    }

    public static MsLlHookStruct PtrToMouseStruct(IntPtr lParam) => Marshal.PtrToStructure<MsLlHookStruct>(lParam);

    public static IntPtr SetHook(LowLevelMouseProc proc)
    {
        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule!;
        return SetWindowsHookEx(WhMouseLl, proc, GetModuleHandle(module.ModuleName), 0);
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfoEx lpmi);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr MonitorFromPoint(PointStruct pt, uint dwFlags);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "Shell_NotifyIconW")]
    public static extern bool ShellNotifyIcon(uint dwMessage, ref NotifyIconData lpdata);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetCursorPos(out PointStruct lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    public static MonitorInfoEx GetMonitorInfo(IntPtr monitorHandle)
    {
        var info = new MonitorInfoEx
        {
            cbSize = (uint)Marshal.SizeOf<MonitorInfoEx>(),
            szDevice = string.Empty
        };

        if (!GetMonitorInfo(monitorHandle, ref info))
        {
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }

        return info;
    }
}
