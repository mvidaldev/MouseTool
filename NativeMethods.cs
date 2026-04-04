using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MouseTool;

internal static class NativeMethods
{
    private const int WhMouseLl = 14;
    private const uint MiWpSignature = 0xFF515700;
    private const uint MiWpMask = 0xFFFFFF00;

    public delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

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
    public struct MsLlHookStruct
    {
        public PointStruct pt;
        public int mouseData;
        public int flags;
        public int time;
        public nuint dwExtraInfo;
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
}
