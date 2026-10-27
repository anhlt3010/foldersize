using FolderSize.Core.Scanning;
using FolderSize.Core.Presentation;
using Xunit;

namespace FolderSize.Tests;

public class ProjectionTests
{
    [Fact]
    public async Task SortSiblingsDescendingWithStableTies()
    {
        var fs = new FakeFileSystem(); fs.Directory("root", "root/z", "root/b", "root/A1", "root/a2");
        fs.File("root/z", 100); fs.File("root/b", 200); fs.File("root/A1", 100); fs.File("root/a2", 100);
        fs.Entries["root/A1"] = fs.Entries["root/A1"] with { Name = "A" };
        fs.Entries["root/a2"] = fs.Entries["root/a2"] with { Name = "a" };
        var root = (await new DirectoryScanner(fs).ScanAsync("root", null, default)).Root;
        var expanded = new HashSet<string> { "root" };
        Assert.Equal(new[] { "root", "b", "A", "a", "z" }, TreeProjection.Flatten(root, expanded, true).Select(x => x.Node.Name));
        Assert.Equal(new[] { "root", "z", "b", "A", "a" }, TreeProjection.Flatten(root, expanded, false).Select(x => x.Node.Name));
    }

    [Fact]
    public async Task ShareOfEmptyParentIsNull()
    {
        var fs = new FakeFileSystem(); fs.Directory("root", "root/empty"); fs.Directory("root/empty");
        var root = (await new DirectoryScanner(fs).ScanAsync("root", null, default)).Root;
        Assert.All(TreeProjection.Flatten(root, new HashSet<string> { "root" }, true), row => Assert.Null(row.ParentShare));
    }

    [Fact]
    public async Task FileCountsDoNotIncludeDirectories()
    {
        var fs = new FakeFileSystem(); fs.Directory("root", "root/sub", "root/file"); fs.Directory("root/sub"); fs.File("root/file", 10);
        var root = (await new DirectoryScanner(fs).ScanAsync("root", null, default)).Root;
        var rows = TreeProjection.Flatten(root, new HashSet<string> { "root" }, true);
        Assert.Equal(1, rows[0].Node.FileCount); Assert.Equal(1, rows[0].Node.DirectoryCount);
        Assert.Equal(100d, rows[1].ParentShare);
    }

    [Fact]
    public void Format1024IsOneKiB()
    {
        Assert.Equal("1 KiB", SizeFormatter.Format(1024));
        Assert.Equal("1,5 KiB", SizeFormatter.Format(1536));
        Assert.Equal("0 B", SizeFormatter.Format(0));
    }

    [Fact]
    public async Task FlattenOnlyExpandedBranches()
    {
        var fs = new FakeFileSystem(); fs.Directory("root", "root/sub"); fs.Directory("root/sub", "root/sub/file"); fs.File("root/sub/file", 10);
        var root = (await new DirectoryScanner(fs).ScanAsync("root", null, default)).Root;
        var expanded = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "root" };
        Assert.Equal(2, TreeProjection.Flatten(root, expanded, true).Count);
        expanded.Add("ROOT/SUB"); Assert.Equal(3, TreeProjection.Flatten(root, expanded, true).Count);
        expanded.Remove("root/sub"); Assert.Equal(2, TreeProjection.Flatten(root, expanded, true).Count);
    }
}
