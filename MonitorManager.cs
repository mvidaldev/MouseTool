using System.Drawing;
using System.Runtime.InteropServices;

namespace MouseTool;

internal sealed class MonitorInfo
{
    public required string DeviceName { get; init; }
    public required Rectangle Bounds { get; init; }
    public required bool IsPrimary { get; init; }
}

internal static class MonitorManager
{
    public static IReadOnlyList<MonitorInfo> GetAllMonitors()
    {
        var monitors = new List<MonitorInfo>();
        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr monitorHandle, IntPtr hdcMonitor, ref NativeMethods.RectStruct monitorRect, IntPtr dwData) =>
        {
            var info = NativeMethods.GetMonitorInfo(monitorHandle);
            monitors.Add(new MonitorInfo
            {
                DeviceName = info.szDevice,
                Bounds = info.rcMonitor.ToRectangle(),
                IsPrimary = (info.dwFlags & NativeMethods.MonitorInfofPrimary) != 0
            });
            return true;
        }, IntPtr.Zero);

        return monitors;
    }

    public static MonitorInfo? GetPrimaryMonitor() => GetAllMonitors().FirstOrDefault(m => m.IsPrimary);

    public static MonitorInfo? FindByDeviceName(string? deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return null;
        }

        return GetAllMonitors().FirstOrDefault(m => string.Equals(m.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase));
    }

    public static MonitorInfo? FromPoint(Point point)
    {
        var handle = NativeMethods.MonitorFromPoint(new NativeMethods.PointStruct { x = point.X, y = point.Y }, NativeMethods.MonitorDefaulttonearest);
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        var info = NativeMethods.GetMonitorInfo(handle);
        return new MonitorInfo
        {
            DeviceName = info.szDevice,
            Bounds = info.rcMonitor.ToRectangle(),
            IsPrimary = (info.dwFlags & NativeMethods.MonitorInfofPrimary) != 0
        };
    }
}
