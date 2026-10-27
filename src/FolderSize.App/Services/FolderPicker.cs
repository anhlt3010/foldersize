using Microsoft.Win32;
using FolderSize.App.Localization;
namespace FolderSize.App.Services;
internal static class FolderPicker
{
    public static string? Pick()
    {
        var dialog = new OpenFolderDialog { Title = LanguageService.Current["FolderPickerTitle"], Multiselect = false };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
