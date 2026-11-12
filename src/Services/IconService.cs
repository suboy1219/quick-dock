using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QuickDock.Models;

namespace QuickDock.Services;

public static class IconService
{
    private const int SHGFI_ICON = 0x000000100;
    private const int SHGFI_LARGEICON = 0x000000000;
    private const int SIIGBF_BIGGERSIZEOK = 0x00000001;
    private const int SIIGBF_ICONONLY = 0x00000004;

    private static readonly Dictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly string[] Palette =
    [
        "#4C8DFF", "#8B5CF6", "#F97316", "#10B981", "#EC4899", "#06B6D4", "#EAB308", "#EF4444"
    ];

    public static ImageSource? Load(LauncherItem item)
        => Load(item.Path, item.Name);

    public static ImageSource? Load(string path, string name)
    {
        var key = path + "|" + name;
        if (Cache.TryGetValue(key, out var cached)) return cached;

        ImageSource? icon = null;
        if (!string.IsNullOrWhiteSpace(path))
        {
            icon = TryLoadShellIcon(path, 64);
            if (icon is null && !path.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
            {
                icon = TryLoadAssociatedIcon(path);
            }
        }

        icon ??= CreateLetterIcon(name);
        Cache[key] = icon;
        return icon;
    }

    public static void Invalidate(LauncherItem item)
    {
        Cache.Remove(item.Path + "|" + item.Name);
    }

    private static ImageSource? TryLoadShellIcon(string path, int size)
    {
        object? factoryObject = null;
        IntPtr hBitmap = IntPtr.Zero;
        try
        {
            var iid = typeof(IShellItemImageFactory).GUID;
            SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out factoryObject);
            if (factoryObject is not IShellItemImageFactory factory) return null;

            var result = factory.GetImage(new NativeSize(size, size),
                SIIGBF_ICONONLY | SIIGBF_BIGGERSIZEOK, out hBitmap);
            if (result != 0 || hBitmap == IntPtr.Zero) return null;

            var source = Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap, IntPtr.Zero, Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(size, size));
            source.Freeze();
            return source;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (hBitmap != IntPtr.Zero) DeleteObject(hBitmap);
            if (factoryObject is not null && Marshal.IsComObject(factoryObject))
            {
                Marshal.ReleaseComObject(factoryObject);
            }
        }
    }

    private static ImageSource? TryLoadAssociatedIcon(string path)
    {
        var info = new ShFileInfo();
        var result = SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf<ShFileInfo>(),
            SHGFI_ICON | SHGFI_LARGEICON);
        if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero) return null;

        try
        {
            var source = Imaging.CreateBitmapSourceFromHIcon(info.hIcon, Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            DestroyIcon(info.hIcon);
        }
    }

    private static ImageSource CreateLetterIcon(string name)
    {
        const int size = 64;
        var letter = string.IsNullOrWhiteSpace(name) ? "?" : name.Trim()[..1].ToUpperInvariant();
        var hash = 0;
        foreach (var c in name) hash = unchecked(hash * 31 + c);
        var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(
            Palette[Math.Abs(hash) % Palette.Length]);

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRoundedRectangle(new SolidColorBrush(color), null, new Rect(0, 0, size, size), 16, 16);
            var text = new FormattedText(letter, System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Microsoft YaHei UI"), FontStyles.Normal,
                    FontWeights.SemiBold, FontStretches.Normal),
                30, Brushes.White, 1.0);
            dc.DrawText(text, new System.Windows.Point((size - text.Width) / 2, (size - text.Height) / 2 + 1));
        }

        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeSize(int width, int height)
    {
        public readonly int Width = width;
        public readonly int Height = height;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileInfo
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [ComImport]
    [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(NativeSize size, int flags, out IntPtr phbm);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(
        string pszPath, IntPtr pbc, ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out object ppv);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath, uint dwFileAttributes, ref ShFileInfo psfi, uint cbFileInfo, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);
}
