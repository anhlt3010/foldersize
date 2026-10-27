using FolderSize.Core.Scanning;
using Xunit;

namespace FolderSize.Tests;

public class ScanningTests
{
    [Fact]
    public async Task PhysicalFileSystemCountsUnicodeFixture()
    {
        var fixture = Path.Combine(Path.GetTempPath(), "FolderSize-test-" + Guid.NewGuid());
        System.IO.Directory.CreateDirectory(Path.Combine(fixture, "Thư mục con"));
        try
        {
            await System.IO.File.WriteAllBytesAsync(Path.Combine(fixture, "Thư mục con", "dữ liệu.bin"), new byte[1234]);
            var result = await new DirectoryScanner(new PhysicalFileSystem()).ScanAsync(fixture, null, default);
            Assert.Equal(1234, result.Root.SizeBytes); Assert.Equal(1, result.Root.FileCount);
            Assert.Equal(1, result.Root.DirectoryCount); Assert.Empty(result.Issues);
        }
        finally { System.IO.Directory.Delete(fixture, true); }
    }

    [Fact]
    public async Task ReparseNodesAreSkippedWithoutEnumeration()
    {
        var fs = new FakeFileSystem(); fs.Directory("root", "root/link", "root/cloud");
        fs.Directory("root/link", "outside"); fs.File("root/cloud", 999);
        fs.Entries["root/link"] = fs.Entries["root/link"] with { Attributes = FileAttributes.Directory | FileAttributes.ReparsePoint };
        fs.Entries["root/cloud"] = fs.Entries["root/cloud"] with { Attributes = FileAttributes.ReparsePoint };
        fs.Listings["root/link"] = () => throw new InvalidOperationException("Must not follow links");
        var result = await new DirectoryScanner(fs).ScanAsync("root", null, default);
        Assert.Equal(0, result.Root.SizeBytes);
        Assert.All(result.Root.Children, node => Assert.True(node.IsSkipped));
        Assert.True(result.Root.IsIncomplete);
        Assert.Equal(2, result.Issues.Count);
    }

    [Fact]
    public async Task ZeroByteRootHasZeroSize()
    {
        var fs = new FakeFileSystem(); fs.Directory("root");
        var result = await new DirectoryScanner(fs).ScanAsync("root", null, default);
        Assert.Equal(0, result.Root.SizeBytes); Assert.Equal(0, result.Root.FileCount);
        Assert.Equal(0, result.Root.DirectoryCount); Assert.False(result.Root.IsIncomplete);
    }

    [Fact]
    public async Task HardLinkPathsAreCountedSeparately()
    {
        var fs = new FakeFileSystem(); fs.Directory("root", "root/a", "root/b");
        fs.File("root/a", 10); fs.File("root/b", 10);
        var result = await new DirectoryScanner(fs).ScanAsync("root", null, default);
        Assert.Equal(20, result.Root.SizeBytes); Assert.Equal(2, result.Root.FileCount);
    }

    [Fact]
    public async Task CancelReturnsPartialAndCanceled()
    {
        using var cts = new CancellationTokenSource();
        var fs = new FakeFileSystem(); fs.Directory("root", "root/a", "root/b"); fs.File("root/a", 10); fs.File("root/b", 20);
        fs.OnRead = path => { if (path == "root/a") cts.Cancel(); };
        var result = await new DirectoryScanner(fs).ScanAsync("root", null, cts.Token);
        Assert.True(result.IsCanceled); Assert.True(result.Root.IsIncomplete);
        Assert.Equal(10, result.Root.SizeBytes); Assert.Single(result.Root.Children);
    }

    [Fact]
    public async Task FinalProgressIsDetachedFromResult()
    {
        var fs = new FakeFileSystem(); fs.Directory("root", "root/a"); fs.File("root/a", 10);
        var reports = new List<ScanProgress>();
        var result = await new DirectoryScanner(fs).ScanAsync("root", new InlineProgress(reports.Add), default);
        var last = Assert.Single(reports);
        Assert.Equal(10, last.SizeBytes); Assert.NotSame(result.Root, last.Snapshot);
        result.Root.Children.Clear(); Assert.Single(last.Snapshot.Children);
    }

    [Fact]
    public async Task MissingRootThrows()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() => new DirectoryScanner(new FakeFileSystem()).ScanAsync("missing", null, default));
    }

    private sealed class InlineProgress(Action<ScanProgress> report) : IProgress<ScanProgress>
    { public void Report(ScanProgress value) => report(value); }

    [Fact]
    public async Task DeniedBranchMarksAncestorsIncomplete()
    {
        var fs = new FakeFileSystem();
        fs.Directory("root", "root/denied", "root/ok");
        fs.Directory("root/denied");
        fs.Listings["root/denied"] = () => throw new UnauthorizedAccessException("Denied");
        fs.File("root/ok", 2048);
        var result = await new DirectoryScanner(fs).ScanAsync("root", null, default);
        Assert.Equal(2048, result.Root.SizeBytes);
        Assert.True(result.Root.IsIncomplete);
        Assert.Single(result.Issues);
    }

    [Fact]
    public async Task EnumerationFailsMidwayKeepsKnownChildren()
    {
        var fs = new FakeFileSystem();
        fs.Directory("root"); fs.File("root/ok", 42);
        fs.Listings["root"] = Broken;
        var result = await new DirectoryScanner(fs).ScanAsync("root", null, default);
        Assert.Equal(42, result.Root.SizeBytes);
        Assert.True(result.Root.IsIncomplete);
        Assert.Single(result.Root.Children);
        Assert.Single(result.Issues);
        static IEnumerable<string> Broken() { yield return "root/ok"; throw new IOException("Interrupted"); }
    }

    [Fact]
    public async Task VanishedFileIsIssue()
    {
        var fs = new FakeFileSystem(); fs.Directory("root", "root/missing", "root/ok"); fs.File("root/ok", 42);
        var result = await new DirectoryScanner(fs).ScanAsync("root", null, default);
        Assert.Equal(42, result.Root.SizeBytes);
        Assert.True(result.Root.IsIncomplete);
        Assert.Equal("root/missing", Assert.Single(result.Issues).Path);
    }

    [Fact]
    public async Task CountsNestedFiles()
    {
        var fs = new FakeFileSystem();
        fs.Directory("root", "root/a.bin", "root/sub", "root/empty");
        fs.File("root/a.bin", 1024);
        fs.Directory("root/sub", "root/sub/b.bin");
        fs.File("root/sub/b.bin", 2048);
        fs.Directory("root/empty");
        var result = await new DirectoryScanner(fs).ScanAsync("root", null, default);
        Assert.Equal(3072, result.Root.SizeBytes);
        Assert.Equal(2, result.Root.FileCount);
        Assert.Equal(2, result.Root.DirectoryCount);
        Assert.Equal(2048, result.Root.Children.Single(x => x.Name == "sub").SizeBytes);
    }
}

internal sealed class FakeFileSystem : IFileSystem
{
    public Dictionary<string, FileEntry> Entries { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, Func<IEnumerable<string>>> Listings { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Action<string>? OnRead { get; set; }
    public void Directory(string path, params string[] children)
    {
        Entries[path] = new(path, Path.GetFileName(path), true, FileAttributes.Directory, 0);
        Listings[path] = () => children;
    }
    public void File(string path, long size) => Entries[path] = new(path, Path.GetFileName(path), false, FileAttributes.Normal, size);
    public FileEntry ReadEntry(string path) { OnRead?.Invoke(path); return Entries.TryGetValue(path, out var entry) ? entry : throw new FileNotFoundException(path); }
    public IEnumerable<string> Enumerate(string directory) => Listings[directory]();
}
