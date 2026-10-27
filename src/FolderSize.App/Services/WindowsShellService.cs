using System.IO;
using FolderSize.Core.Actions;
using FolderSize.Core.Scanning;
namespace FolderSize.App.Services;

public interface IRecycleBackend
{
    RecycleResult Recycle(string path, CancellationToken token);
}
public sealed class WindowsShellService(IFileSystem? fileSystem = null, IRecycleBackend? backend = null) : IShellService
{
    private readonly IFileSystem fs = fileSystem ?? new PhysicalFileSystem();
    public Task OpenInExplorerAsync(string path) => Sta.Run(() => { ShellInterop.SelectInExplorer(Path.GetFullPath(path)); return true; });
    public Task<RecycleResult> RecycleAsync(string root, string path, CancellationToken token) => Sta.Run(() =>
    {
        if (token.IsCancellationRequested) return new RecycleResult(RecycleStatus.Canceled);
        var validation = PathPolicy.ValidateRecycle(root, path, fs);
        if (!validation.Allowed) return new RecycleResult(RecycleStatus.Failed, validation.Reason);
        return (backend ?? new ShellRecycleBackend()).Recycle(Path.GetFullPath(path), token);
    });
}
internal static class Sta
{
    public static Task<T> Run<T>(Func<T> action)
    {
        var source = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => { try { source.SetResult(action()); } catch (Exception e) { source.SetException(e); } }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); return source.Task;
    }
}
