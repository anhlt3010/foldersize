using System.Windows.Media;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace FolderSize.App.Services;

public sealed class WindowsIconProvider
{
    private const int CacheLimit = 128;
    private readonly Dictionary<string, ImageSource?> cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object gate = new();

    public ImageSource? GetIcon(string path, bool isDirectory, bool isExpanded)
    {
        var isDrive = isDirectory && string.Equals(Path.TrimEndingDirectorySeparator(path),
            Path.TrimEndingDirectorySeparator(Path.GetPathRoot(path) ?? ""), StringComparison.OrdinalIgnoreCase);
        var key = isDrive ? "stock:drive" : isDirectory ? isExpanded ? "stock:folder-open" : "stock:folder" : "file:" + Path.GetExtension(path);
        lock (gate)
        {
            if (cache.TryGetValue(key, out var image)) return image;
            // Stock icons never query the real folder/drive. File type lookup uses only
            // an extension and SHGFI_USEFILEATTRIBUTES: no file reads or cloud hydration.
            image = isDrive ? LoadStock(8) : isDirectory ? LoadStock(isExpanded ? 4 : 3) : LoadFileType(Path.GetExtension(path));
            image ??= LoadStock(0);
            if (cache.Count >= CacheLimit) cache.Remove(cache.Keys.First());
            cache.Add(key, image);
            return image;
        }
    }

    private static ImageSource? LoadFileType(string extension)
    {
        var result = SHGetFileInfo("item" + extension, 0x80, out var info, (uint)Marshal.SizeOf<FileInfo>(), 0x100 | 0x10);
        return CopyAndRelease(result == IntPtr.Zero ? IntPtr.Zero : info.Icon);
    }
    private static ImageSource? LoadStock(int id)
    {
        var info = new StockIconInfo { Size = (uint)Marshal.SizeOf<StockIconInfo>() };
        var result = SHGetStockIconInfo(id, 0x100, ref info);
        return CopyAndRelease(result < 0 ? IntPtr.Zero : info.Icon);
    }
    private static BitmapSource? CopyAndRelease(IntPtr icon)
    {
        if (icon == IntPtr.Zero) return null;
        try
        {
            var bitmap = Imaging.CreateBitmapSourceFromHIcon(icon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            bitmap.Freeze(); return bitmap;
        }
        finally { DestroyIcon(icon); }
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct FileInfo
    {
        public IntPtr Icon;
        public int IconIndex;
        public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string TypeName;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StockIconInfo
    {
        public uint Size;
        public IntPtr Icon;
        public int SystemImageIndex;
        public int IconIndex;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Path;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHGetFileInfoW")]
    private static extern IntPtr SHGetFileInfo(string path, uint attributes, out FileInfo info, uint size, uint flags);
    [DllImport("shell32.dll")]
    private static extern int SHGetStockIconInfo(int id, uint flags, ref StockIconInfo info);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr icon);
}
