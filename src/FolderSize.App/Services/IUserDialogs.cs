namespace FolderSize.App.Services;
public interface IUserDialogs
{
    Task<bool> ConfirmRecycleAsync(string path, long scannedBytes);
    void ShowError(string title, string message);
}
