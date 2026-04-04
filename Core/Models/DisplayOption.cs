
namespace MouseTool;

internal sealed class DisplayOption
{
    public required string DeviceName { get; init; }
    public required string DisplayName { get; init; }

    public static DisplayOption FromMonitor(MonitorInfo screen, int index, string role)
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

