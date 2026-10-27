using System.Collections.ObjectModel;
using System.IO;
using FolderSize.Core.Scanning;
using FolderSize.Core.Actions;
using FolderSize.App.Services;
using FolderSize.App.Infrastructure;
using FolderSize.Core.Presentation;
using System.Windows.Input;
using FolderSize.App.Localization;
namespace FolderSize.App.ViewModels;
public enum AppState { Empty, Scanning, Completed, Canceled, Recycling, Failed, Stale }
public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly DirectoryScanner scanner;
    private readonly IShellService shell;
    private readonly IUserDialogs dialogs;
    private readonly LanguageService language;
    private readonly object stateGate = new();
    public MainViewModel(DirectoryScanner scanner, IShellService shell, IUserDialogs dialogs, LanguageService? language = null)
    {
        this.scanner = scanner; this.shell = shell; this.dialogs = dialogs; this.language = language ?? new();
        this.language.LanguageChanged += OnLanguageChanged;
    }
    public void Dispose() { language.LanguageChanged -= OnLanguageChanged; cancellation?.Cancel(); }
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
    private string? statusKey;
    private object?[] statusArguments = [];
    public string StatusText => statusKey is null ? "" : language.Format(statusKey,
        statusArguments.Select(value => value is string message ? language.TranslateKnownMessage(message) : value).ToArray());
    public IReadOnlyList<ScanIssue> Issues { get; private set; } = [];
    public bool CanScan => State is not (AppState.Scanning or AppState.Recycling);
    public bool CanCancel => State == AppState.Scanning;
    public bool IsEmpty => Rows.Count == 0;
    public string TotalSize => root is null ? "—" : SizeFormatter.Format(root.SizeBytes, language.Culture);
    public string TotalFiles => root?.FileCount.ToString("N0", language.Culture) ?? "—";
    public string TotalDirectories => root?.DirectoryCount.ToString("N0", language.Culture) ?? "—";
    public string IssueLabel => language.Format("IssuesLabel", Issues.Count);
    public string StateLabel => language[State switch { AppState.Empty => "StateEmpty", AppState.Scanning => "StateScanning", AppState.Completed => root?.IsIncomplete == true ? "StateIncomplete" : "StateCompleted", AppState.Canceled => "StateCanceled", AppState.Recycling => "StateRecycling", AppState.Stale => "StateStale", _ => "StateFailed" }];
    public string RecycleReason => language[CanRecycle ? "RecycleHint" : SelectedRow is null ? "NoSelectionHint" : string.Equals(SelectedRow.Node.FullPath, scanRoot, StringComparison.OrdinalIgnoreCase) ? "RootRecycleHint" : "BlockedRecycleHint"];
    private void SetStatus(string key, params object?[] arguments) { statusKey = key; statusArguments = arguments; Notify(nameof(StatusText)); }
    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        lock (stateGate)
        {
            foreach (var row in Rows) row.RefreshLocalized();
            foreach (var name in new[] { nameof(TotalSize), nameof(TotalFiles), nameof(TotalDirectories), nameof(StateLabel), nameof(StatusText), nameof(IssueLabel), nameof(RecycleReason), nameof(SelectedRow) }) Notify(name);
        }
    }
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
            if (!Path.IsPathFullyQualified(PathInput)) throw new ArgumentException(language["AbsolutePathRequired"]);
            var newRoot = Path.GetFullPath(PathInput);
            if (!string.Equals(scanRoot, newRoot, StringComparison.OrdinalIgnoreCase)) { expanded.Clear(); SelectedRow = null; }
            scanRoot = newRoot; expanded.Add(scanRoot);
            // A WPF/UI synchronization context serializes progress and completion.
            // Without one, Progress<T> posts concurrent thread-pool callbacks which
            // can outlive the generation check and clear a newly selected row.
            IProgress<ScanProgress>? progress = SynchronizationContext.Current is null ? null : new Progress<ScanProgress>(report =>
            {
                lock (stateGate)
                {
                    if (generation != scanGeneration || State != AppState.Scanning) return;
                    root = report.Snapshot; RefreshRows(false);
                    SetStatus("ScanningPath", report.CurrentPath);
                }
            });
            var result = await scanner.ScanAsync(scanRoot, progress, cancellation.Token);
            lock (stateGate)
            {
                ++generation;
                root = result.Root; Issues = result.Issues; RefreshRows(!result.IsCanceled);
                State = result.IsCanceled ? needsFreshScan ? AppState.Stale : AppState.Canceled : AppState.Completed;
                if (!result.IsCanceled) needsFreshScan = false;
                SetStatus(result.IsCanceled ? "ScanCanceled" : root.IsIncomplete ? "ScanIncomplete" : "ScanCompleted");
            }
        }
        catch (Exception e)
        {
            lock (stateGate) { ++generation; State = root is null ? AppState.Failed : AppState.Stale; SetStatus("ScanFailed", e.Message); }
            dialogs.ShowError(language["CannotScan"], language.TranslateKnownMessage(e.Message));
        }
        finally
        { lock (stateGate) { ++generation; cancellation.Dispose(); cancellation = null; Notify(nameof(StatusText)); Notify(nameof(Issues)); } }
    }
    public void Cancel() => cancellation?.Cancel();
    public void ToggleExpanded(NodeRowViewModel row)
    {
        lock (stateGate)
        {
            if (!row.Row.HasChildren) return;
            if (!expanded.Remove(row.Node.FullPath)) expanded.Add(row.Node.FullPath);
            RefreshRows(State == AppState.Completed);
        }
    }
    private void RefreshRows(bool sort)
    {
        if (root is null) return;
        var existing = Rows.ToDictionary(x => x.Node.FullPath, StringComparer.OrdinalIgnoreCase);
        var target = TreeProjection.Flatten(root, expanded, sort).Select(row =>
        {
            if (existing.TryGetValue(row.Node.FullPath, out var vm)) { vm.Update(row); return vm; }
            return new NodeRowViewModel(row, language);
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
            SetStatus(result.Status == RecycleStatus.Success ? "RecycledRescan" : "ActionUnfinished");
            if (result.Status != RecycleStatus.Success) dialogs.ShowError(language["RecycleResult"], language.TranslateKnownMessage(result.Message ?? StatusText));
            if (result.Status is RecycleStatus.Success or RecycleStatus.Partial)
            { PathInput = scanRoot; await ScanAsync(); }
        }
        catch (Exception e) { State = AppState.Stale; SetStatus("StateStale"); dialogs.ShowError(language["CannotRecycle"], language.TranslateKnownMessage(e.Message)); }
        finally { Notify(nameof(StatusText)); }
    }
    public async Task OpenSelectedAsync()
    {
        if (SelectedRow is null) return;
        try { await shell.OpenInExplorerAsync(SelectedRow.Node.FullPath); }
        catch (Exception e) { dialogs.ShowError(language["CannotExplorer"], language.TranslateKnownMessage(e.Message)); }
    }
}
