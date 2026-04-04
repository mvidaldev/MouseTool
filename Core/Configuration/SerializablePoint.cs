using System.Drawing;

namespace MouseTool;

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

