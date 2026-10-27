using Microsoft.Win32;
namespace FolderSize.App.Services;
internal static class FolderPicker
{
    public static string? Pick()
    {
        var dialog = new OpenFolderDialog { Title = "Chọn thư mục hoặc ổ đĩa để quét", Multiselect = false };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
