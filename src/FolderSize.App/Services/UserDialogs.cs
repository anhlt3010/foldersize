using System.Windows;
using FolderSize.Core.Presentation;
using FolderSize.App.Localization;
namespace FolderSize.App.Services;
internal sealed class UserDialogs : IUserDialogs
{
    public Task<bool> ConfirmRecycleAsync(string path, long scannedBytes) => Task.FromResult(MessageBox.Show(
        LanguageService.Current.Format("ConfirmBody", path, SizeFormatter.Format(scannedBytes, LanguageService.Current.Culture)),
        LanguageService.Current["ConfirmTitle"], MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes);
    public void ShowError(string title, string message) => MessageBox.Show(LanguageService.Current.TranslateKnownMessage(message), LanguageService.Current.TranslateKnownMessage(title), MessageBoxButton.OK, MessageBoxImage.Information);
}
