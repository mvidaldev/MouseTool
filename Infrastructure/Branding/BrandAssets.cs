using System.Drawing;

namespace MouseTool;

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

