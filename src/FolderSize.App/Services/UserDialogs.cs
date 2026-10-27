using System.Windows;
using FolderSize.Core.Presentation;
namespace FolderSize.App.Services;
internal sealed class UserDialogs : IUserDialogs
{
    public Task<bool> ConfirmRecycleAsync(string path, long scannedBytes) => Task.FromResult(MessageBox.Show(
        $"{path}\n\nKích thước logic tại lần quét: {SizeFormatter.Format(scannedBytes)}\n\nToàn bộ nội dung con của thư mục cũng sẽ được chuyển. Nội dung có thể đã thay đổi từ lần quét.\n\nChuyển vào Thùng rác chưa giải phóng dung lượng ổ đĩa. Tiếp tục?",
        "Chuyển vào Thùng rác?", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes);
    public void ShowError(string title, string message) => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
}
