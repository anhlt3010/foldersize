using System.Collections.ObjectModel;
using System.IO;
using FolderSize.Core.Scanning;
using FolderSize.Core.Actions;
using FolderSize.App.Services;
using FolderSize.App.Infrastructure;
using FolderSize.Core.Presentation;
namespace FolderSize.App.ViewModels;
public enum AppState { Empty, Scanning, Completed, Canceled, Recycling, Failed, Stale }
public sealed class MainViewModel(DirectoryScanner scanner, IShellService shell, IUserDialogs dialogs) : ObservableObject
{
    private string pathInput = "";
    private NodeRowViewModel? selectedRow;
    private AppState state;
    private CancellationTokenSource? cancellation;
    private ScanNode? root;
    private string? scanRoot;
    private long generation;
    private readonly HashSet<string> expanded = new(StringComparer.OrdinalIgnoreCase);
    public string PathInput { get => pathInput; set => Set(ref pathInput, value); }
    public ObservableCollection<NodeRowViewModel> Rows { get; } = [];
    public NodeRowViewModel? SelectedRow { get => selectedRow; set { Set(ref selectedRow, value); Notify(nameof(CanRecycle)); } }
    public AppState State { get => state; private set { Set(ref state, value); Notify(nameof(CanScan)); Notify(nameof(CanCancel)); Notify(nameof(CanRecycle)); } }
    public string StatusText { get; private set; } = "";
    public IReadOnlyList<ScanIssue> Issues { get; private set; } = [];
    public bool CanScan => State is not (AppState.Scanning or AppState.Recycling);
    public bool CanCancel => State == AppState.Scanning;
    public bool CanRecycle => State == AppState.Completed && SelectedRow is { Node.IsSkipped: false } row &&
        scanRoot is not null && !string.Equals(scanRoot, row.Node.FullPath, StringComparison.OrdinalIgnoreCase) &&
        row.Node.FullPath.StartsWith(Path.TrimEndingDirectorySeparator(scanRoot) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    public async Task ScanAsync()
    {
        if (!CanScan) return;
        State = AppState.Scanning;
        var scanGeneration = ++generation;
        cancellation = new();
        try
        {
            if (!Path.IsPathFullyQualified(PathInput)) throw new ArgumentException("Hãy nhập đường dẫn tuyệt đối.");
            var newRoot = Path.GetFullPath(PathInput);
            if (!string.Equals(scanRoot, newRoot, StringComparison.OrdinalIgnoreCase)) { expanded.Clear(); SelectedRow = null; }
            scanRoot = newRoot; expanded.Add(scanRoot);
            var progress = new Progress<ScanProgress>(report =>
            {
                if (generation != scanGeneration || State != AppState.Scanning) return;
                root = report.Snapshot; RefreshRows(false);
                StatusText = $"Đang quét: {report.CurrentPath}"; Notify(nameof(StatusText));
            });
            var result = await scanner.ScanAsync(scanRoot, progress, cancellation.Token);
            ++generation;
            root = result.Root; Issues = result.Issues; RefreshRows(!result.IsCanceled);
            State = result.IsCanceled ? AppState.Canceled : AppState.Completed;
            StatusText = result.IsCanceled ? "Đã hủy — kết quả tạm, chưa đầy đủ." : "Quét hoàn tất.";
        }
        catch (Exception e)
        { State = AppState.Failed; StatusText = e.Message; dialogs.ShowError("Không thể quét", e.Message); }
        finally
        { ++generation; cancellation.Dispose(); cancellation = null; Notify(nameof(StatusText)); Notify(nameof(Issues)); }
    }
    public void Cancel() => cancellation?.Cancel();
    public void ToggleExpanded(NodeRowViewModel row)
    {
        if (!row.Row.HasChildren) return;
        if (!expanded.Remove(row.Node.FullPath)) expanded.Add(row.Node.FullPath);
        RefreshRows(State == AppState.Completed);
    }
    private void RefreshRows(bool sort)
    {
        if (root is null) return;
        var existing = Rows.ToDictionary(x => x.Node.FullPath, StringComparer.OrdinalIgnoreCase);
        var target = TreeProjection.Flatten(root, expanded, sort).Select(row =>
        {
            if (existing.TryGetValue(row.Node.FullPath, out var vm)) { vm.Update(row); return vm; }
            return new NodeRowViewModel(row);
        }).ToArray();
        for (var i = 0; i < target.Length; i++)
        {
            if (i < Rows.Count && ReferenceEquals(Rows[i], target[i])) continue;
            var previous = Rows.IndexOf(target[i]);
            if (previous >= 0) Rows.Move(previous, i); else Rows.Insert(i, target[i]);
        }
        while (Rows.Count > target.Length) Rows.RemoveAt(Rows.Count - 1);
        if (SelectedRow is not null && !Rows.Contains(SelectedRow)) SelectedRow = null;
    }
    public Task RecycleSelectedAsync() => Task.CompletedTask;
    public async Task OpenSelectedAsync()
    {
        if (SelectedRow is null) return;
        try { await shell.OpenInExplorerAsync(SelectedRow.Node.FullPath); }
        catch (Exception e) { dialogs.ShowError("Không thể mở Explorer", e.Message); }
    }
}
