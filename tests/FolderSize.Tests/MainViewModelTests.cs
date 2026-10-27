using FolderSize.App.ViewModels;
using FolderSize.App.Services;
using FolderSize.Core.Actions;
using FolderSize.Core.Scanning;
using Xunit;

namespace FolderSize.Tests;
public class MainViewModelTests
{
    [Fact]
    public async Task OpeningLargeBranchBatchesCollectionChanges()
    {
        var fs = new FakeFileSystem(); var sub = Path.Combine(Root, "sub");
        var children = Enumerable.Range(0, 1000).Select(i => Path.Combine(sub, $"file{i}")).ToArray();
        fs.Directory(Root, sub); fs.Directory(sub, children); foreach (var child in children) fs.File(child, 1);
        var vm = new MainViewModel(new(fs), new FakeShell(), new FakeDialogs()) { PathInput = Root };
        await vm.ScanAsync(); vm.SelectedRow = vm.Rows[1]; var selected = vm.SelectedRow;
        var changes = 0; vm.Rows.CollectionChanged += (_, _) => changes++;
        vm.ToggleExpanded(vm.Rows[1]);
        Assert.Equal(1002, vm.Rows.Count); Assert.Same(selected, vm.SelectedRow);
        Assert.InRange(changes, 1, 2);
    }
    [Fact]
    public async Task CanceledConfirmationDoesNotCallShell()
    {
        var shell = new FakeShell(); var vm = new MainViewModel(new(Fixture()), shell, new FakeDialogs { Confirm = false }) { PathInput = Root };
        await vm.ScanAsync(); vm.SelectedRow = vm.Rows[1]; await vm.RecycleSelectedAsync();
        Assert.Empty(shell.Recycled); Assert.Equal(AppState.Completed, vm.State);
    }
    [Theory]
    [InlineData(RecycleStatus.Canceled)][InlineData(RecycleStatus.Failed)]
    public async Task ShellCancelOrFailureIsNotSuccess(RecycleStatus status)
    {
        var dialogs = new FakeDialogs(); var shell = new FakeShell { Result = new(status, "Uncertain") };
        var vm = new MainViewModel(new(Fixture()), shell, dialogs) { PathInput = Root };
        await vm.ScanAsync(); vm.SelectedRow = vm.Rows[1]; await vm.RecycleSelectedAsync();
        Assert.Equal(new[] { Path.Combine(Root, "file") }, shell.Recycled);
        Assert.Equal(AppState.Stale, vm.State); Assert.False(vm.CanRecycle); Assert.Single(dialogs.Errors);
    }
    [Fact]
    public async Task ProgressDoesNotReplaceSelectionUnnecessarily()
    {
        var vm = new MainViewModel(new(Fixture()), new FakeShell(), new FakeDialogs()) { PathInput = Root };
        await vm.ScanAsync(); vm.SelectedRow = vm.Rows[1]; var selected = vm.SelectedRow;
        var context = new QueuedContext(); var previous = SynchronizationContext.Current;
        Task scan;
        try { SynchronizationContext.SetSynchronizationContext(context); scan = vm.ScanAsync(); }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
        Assert.True(SpinWait.SpinUntil(() => context.Count >= 2, TimeSpan.FromSeconds(5)), "Expected progress then completion");
        context.RunFirst(); Assert.Same(selected, vm.SelectedRow); Assert.Same(selected, vm.Rows[1]);
        context.RunAll(); await scan; Assert.Same(selected, vm.SelectedRow);
    }

    [Fact]
    public async Task LateProgressCannotOverwriteNewerCompletedScan()
    {
        var fs = Fixture(); var vm = new MainViewModel(new(fs), new FakeShell(), new FakeDialogs()) { PathInput = Root };
        var context = new QueuedContext(); var previous = SynchronizationContext.Current;
        Task scan;
        try { SynchronizationContext.SetSynchronizationContext(context); scan = vm.ScanAsync(); }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
        Assert.True(SpinWait.SpinUntil(() => context.Count >= 2, TimeSpan.FromSeconds(5)), "Expected queued progress");
        context.RunLast(); await scan;
        fs.File(Path.Combine(Root, "file"), 123);
        await vm.ScanAsync();
        context.RunAll(); Assert.Equal(123, vm.Rows[0].Node.SizeBytes); Assert.Equal(AppState.Completed, vm.State);
    }

    private static readonly string Root = Path.GetFullPath(".work/test-root");
    private static FakeFileSystem Fixture()
    {
        var fs = new FakeFileSystem(); fs.Directory(Root, Path.Combine(Root, "file")); fs.File(Path.Combine(Root, "file"), 42); return fs;
    }
    [Fact]
    public async Task CompletionEnablesOnlyEligibleSelection()
    {
        var vm = new MainViewModel(new(Fixture()), new FakeShell(), new FakeDialogs()) { PathInput = Root };
        await vm.ScanAsync(); Assert.Equal(AppState.Completed, vm.State);
        vm.SelectedRow = vm.Rows[0]; Assert.False(vm.CanRecycle);
        vm.SelectedRow = vm.Rows[1]; Assert.True(vm.CanRecycle);
    }
    [Fact]
    public async Task InvalidPathShowsError()
    {
        var dialogs = new FakeDialogs(); var vm = new MainViewModel(new(Fixture()), new FakeShell(), dialogs) { PathInput = "relative" };
        await vm.ScanAsync(); Assert.Equal(AppState.Failed, vm.State); Assert.Single(dialogs.Errors); Assert.Empty(vm.Rows);
    }
    [Fact]
    public async Task BusyPreventsSecondScan()
    {
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        var fs = Fixture(); var reads = 0;
        fs.OnRead = path => { if (path == Root) { Interlocked.Increment(ref reads); entered.Set(); release.Wait(TimeSpan.FromSeconds(10)); } };
        var vm = new MainViewModel(new(fs), new FakeShell(), new FakeDialogs()) { PathInput = Root };
        var scan = vm.ScanAsync();
        try { Assert.True(entered.Wait(TimeSpan.FromSeconds(5))); Assert.False(vm.CanScan); await vm.ScanAsync(); Assert.Equal(1, reads); }
        finally { release.Set(); await scan; }
    }
    [Fact]
    public async Task CancelKeepsRowsAndDisablesRecycle()
    {
        var fs = Fixture(); MainViewModel? vm = null;
        fs.OnRead = path => { if (path != Root) vm!.Cancel(); };
        vm = new MainViewModel(new(fs), new FakeShell(), new FakeDialogs()) { PathInput = Root };
        await vm.ScanAsync(); Assert.Equal(AppState.Canceled, vm.State); Assert.NotEmpty(vm.Rows);
        vm.SelectedRow = vm.Rows.Last(); Assert.False(vm.CanRecycle);
    }
}
internal sealed class QueuedContext : SynchronizationContext
{
    private readonly List<(SendOrPostCallback Callback, object? State)> queue = [];
    public int Count { get { lock (queue) return queue.Count; } }
    public override void Post(SendOrPostCallback d, object? state) { lock (queue) queue.Add((d, state)); }
    public void RunFirst() => Run(0);
    public void RunLast() => Run(Count - 1);
    private void Run(int index)
    {
        (SendOrPostCallback Callback, object? State) item;
        lock (queue) { item = queue[index]; queue.RemoveAt(index); }
        item.Callback(item.State);
    }
    public void RunAll() { while (Count > 0) RunFirst(); }
}
internal sealed class FakeDialogs : IUserDialogs
{
    public List<string> Errors { get; } = [];
    public bool Confirm { get; set; } = true;
    public Task<bool> ConfirmRecycleAsync(string path, long scannedBytes) => Task.FromResult(Confirm);
    public void ShowError(string title, string message) => Errors.Add(message);
}
internal sealed class FakeShell : IShellService
{
    public List<string> Recycled { get; } = [];
    public RecycleResult Result { get; set; } = new(RecycleStatus.Success);
    public Task OpenInExplorerAsync(string path) => Task.CompletedTask;
    public Task<RecycleResult> RecycleAsync(string root, string path, CancellationToken token) { Recycled.Add(path); return Task.FromResult(Result); }
}
