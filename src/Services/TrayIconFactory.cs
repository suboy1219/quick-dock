using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace QuickDock.Services;

public static class TrayIconFactory
{
    public static Icon Create()
    {
        using var bitmap = new Bitmap(64, 64);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);

            using var path = RoundedRect(new RectangleF(2, 2, 60, 60), 16);
            using var brush = new LinearGradientBrush(new RectangleF(2, 2, 60, 60),
                Color.FromArgb(255, 76, 141, 255), Color.FromArgb(255, 139, 92, 246), 45f);
            graphics.FillPath(brush, path);

            using var white = new SolidBrush(Color.White);
            graphics.FillRectangle(white, 15, 15, 15, 15);
            graphics.FillRectangle(white, 34, 15, 15, 15);
            graphics.FillRectangle(white, 15, 34, 15, 15);
            graphics.FillRectangle(white, 34, 34, 15, 15);
        }

        var handle = bitmap.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(handle);
            return (Icon)temp.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    private static GraphicsPath RoundedRect(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);
}
