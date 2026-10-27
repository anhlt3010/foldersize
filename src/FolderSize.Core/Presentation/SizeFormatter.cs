namespace FolderSize.Core.Presentation;
public static class SizeFormatter
{
    public static string Format(long bytes, System.Globalization.CultureInfo? culture = null)
    {
        string[] units = ["B", "KiB", "MiB", "GiB", "TiB"];
        double size = bytes; var index = 0;
        while (size >= 1024 && index < units.Length - 1) { size /= 1024; index++; }
        return size.ToString("0.##", culture ?? System.Globalization.CultureInfo.GetCultureInfo("vi-VN")) + " " + units[index];
    }
}
