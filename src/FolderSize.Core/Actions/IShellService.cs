namespace FolderSize.Core.Actions;
public enum RecycleStatus { Success, Canceled, Failed, Partial }
public sealed record RecycleResult(RecycleStatus Status, string? Message = null);
public interface IShellService
{
    Task OpenInExplorerAsync(string path);
    Task<RecycleResult> RecycleAsync(string root, string path, CancellationToken token);
}
