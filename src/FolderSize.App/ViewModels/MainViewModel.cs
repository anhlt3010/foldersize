using System.Collections.ObjectModel;
using System.IO;
using FolderSize.Core.Scanning;
using FolderSize.Core.Actions;
using FolderSize.App.Services;
using FolderSize.App.Infrastructure;
using FolderSize.Core.Presentation;
using System.Windows.Input;
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
    private bool needsFreshScan;
    private readonly HashSet<string> expanded = new(StringComparer.OrdinalIgnoreCase);
    public string PathInput { get => pathInput; set => Set(ref pathInput, value); }
    public BatchCollection<NodeRowViewModel> Rows { get; } = [];
    public NodeRowViewModel? SelectedRow { get => selectedRow; set { Set(ref selectedRow, value); Notify(nameof(CanRecycle)); Notify(nameof(RecycleReason)); CommandManager.InvalidateRequerySuggested(); } }
    public AppState State { get => state; private set { Set(ref state, value); foreach (var name in new[] { nameof(CanScan), nameof(CanCancel), nameof(CanRecycle), nameof(StateLabel), nameof(RecycleReason), nameof(IsEmpty) }) Notify(name); CommandManager.InvalidateRequerySuggested(); } }
    public string StatusText { get; private set; } = "";
    public IReadOnlyList<ScanIssue> Issues { get; private set; } = [];
    public bool CanScan => State is not (AppState.Scanning or AppState.Recycling);
    public bool CanCancel => State == AppState.Scanning;
    public bool IsEmpty => Rows.Count == 0;
    public string TotalSize => root is null ? "—" : SizeFormatter.Format(root.SizeBytes);
    public string TotalFiles => root?.FileCount.ToString("N0") ?? "—";
    public string TotalDirectories => root?.DirectoryCount.ToString("N0") ?? "—";
    public string IssueLabel => $"{Issues.Count:N0} mục bị bỏ qua / lỗi · Xem chi tiết";
    public string StateLabel => State switch { AppState.Empty => "Chọn vị trí để bắt đầu", AppState.Scanning => "Đang quét…", AppState.Completed => root?.IsIncomplete == true ? "Hoàn tất · kết quả chưa đầy đủ" : "Quét hoàn tất", AppState.Canceled => "Đã hủy · kết quả tạm", AppState.Recycling => "Đang chuyển vào Thùng rác…", AppState.Stale => "Cần quét lại", _ => "Không thể quét" };
    public string RecycleReason => CanRecycle ? "Thư mục bao gồm toàn bộ nội dung con. Dữ liệu có thể đã thay đổi từ lần quét." : SelectedRow is null ? "Chọn một mục trong bảng để xem chi tiết." : string.Equals(SelectedRow.Node.FullPath, scanRoot, StringComparison.OrdinalIgnoreCase) ? "Không thể xóa gốc vùng quét. Hãy quét thư mục cha nếu cần quản lý mục này." : "Chỉ chuyển vào Thùng rác sau khi quét hoàn tất; liên kết bị bỏ qua không được xóa.";
    private RelayCommand? scanCommand, cancelCommand, recycleCommand, openCommand, toggleCommand;
    public ICommand ScanCommand => scanCommand ??= new(_ => ScanAsync(), _ => CanScan);
    public ICommand CancelCommand => cancelCommand ??= new(_ => { Cancel(); return Task.CompletedTask; }, _ => CanCancel);
    public ICommand RecycleCommand => recycleCommand ??= new(_ => RecycleSelectedAsync(), _ => CanRecycle);
    public ICommand OpenCommand => openCommand ??= new(_ => OpenSelectedAsync(), _ => SelectedRow is not null && CanScan);
    public ICommand ToggleCommand => toggleCommand ??= new(p => { if (p is NodeRowViewModel row) ToggleExpanded(row); return Task.CompletedTask; });
    public bool CanRecycle => State == AppState.Completed && SelectedRow is { Node.IsSkipped: false } row &&
        scanRoot is not null && !string.Equals(scanRoot, row.Node.FullPath, StringComparison.OrdinalIgnoreCase) &&
        row.Node.FullPath.StartsWith(Path.EndsInDirectorySeparator(scanRoot) ? scanRoot : scanRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
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
            // A WPF/UI synchronization context serializes progress and completion.
            // Without one, Progress<T> posts concurrent thread-pool callbacks which
            // can outlive the generation check and clear a newly selected row.
            IProgress<ScanProgress>? progress = SynchronizationContext.Current is null ? null : new Progress<ScanProgress>(report =>
            {
                if (generation != scanGeneration || State != AppState.Scanning) return;
                root = report.Snapshot; RefreshRows(false);
                StatusText = $"Đang quét: {report.CurrentPath}"; Notify(nameof(StatusText));
            });
            var result = await scanner.ScanAsync(scanRoot, progress, cancellation.Token);
            ++generation;
            root = result.Root; Issues = result.Issues; RefreshRows(!result.IsCanceled);
            State = result.IsCanceled ? needsFreshScan ? AppState.Stale : AppState.Canceled : AppState.Completed;
            if (!result.IsCanceled) needsFreshScan = false;
            StatusText = result.IsCanceled ? "Đã hủy — kết quả tạm, chưa đầy đủ." : root.IsIncomplete ? "Quét hoàn tất. Một số mục chưa đọc được; xem chi tiết lỗi." : "Quét hoàn tất. Dung lượng là kích thước logic tại thời điểm quét.";
        }
        catch (Exception e)
        { State = root is null ? AppState.Failed : AppState.Stale; StatusText = "Không thể quét: " + e.Message; dialogs.ShowError("Không thể quét", e.Message); }
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
        var selection = SelectedRow;
        // A reset batches large branch changes. Reuse row instances and restore selection
        // after WPF processes the collection notification synchronously.
        if (Rows.Count != target.Length || !Rows.SequenceEqual(target)) Rows.Replace(target);
        SelectedRow = selection is not null && target.Contains(selection) ? selection : null;
        foreach (var name in new[] { nameof(TotalSize), nameof(TotalFiles), nameof(TotalDirectories), nameof(IsEmpty), nameof(IssueLabel), nameof(SelectedRow) }) Notify(name);
    }
    public async Task RecycleSelectedAsync()
    {
        if (!CanRecycle || SelectedRow is null || scanRoot is null) return;
        var node = SelectedRow.Node;
        State = AppState.Recycling;
        try
        {
            if (!await dialogs.ConfirmRecycleAsync(node.FullPath, node.SizeBytes)) { State = AppState.Completed; return; }
            needsFreshScan = true;
            var result = await shell.RecycleAsync(scanRoot, node.FullPath, CancellationToken.None);
            State = AppState.Stale;
            StatusText = result.Status == RecycleStatus.Success ? "Đã chuyển vào Thùng rác. Cần quét lại." : "Thao tác chưa hoàn tất. Cần quét lại.";
            if (result.Status != RecycleStatus.Success) dialogs.ShowError("Kết quả Thùng rác", result.Message ?? StatusText);
            if (result.Status is RecycleStatus.Success or RecycleStatus.Partial)
            { PathInput = scanRoot; await ScanAsync(); }
        }
        catch (Exception e) { State = AppState.Stale; StatusText = "Cần quét lại."; dialogs.ShowError("Không thể chuyển vào Thùng rác", e.Message); }
        finally { Notify(nameof(StatusText)); }
    }
    public async Task OpenSelectedAsync()
    {
        if (SelectedRow is null) return;
        try { await shell.OpenInExplorerAsync(SelectedRow.Node.FullPath); }
        catch (Exception e) { dialogs.ShowError("Không thể mở Explorer", e.Message); }
    }
}
