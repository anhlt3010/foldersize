using FolderSize.App.Services;
using FolderSize.Core.Actions;
using Xunit;
namespace FolderSize.Tests;
public class ShellServiceTests
{
    [Fact]
    public async Task RevalidationFailureNeverCallsBackend()
    {
        var fs = new FakeFileSystem(); fs.Directory(@"D:\"); fs.Directory(@"D:\data"); fs.File(@"D:\data\file", 10);
        fs.Entries[@"D:\data\file"] = fs.Entries[@"D:\data\file"] with { Attributes = FileAttributes.ReparsePoint };
        var backend = new RecordingBackend();
        var result = await new WindowsShellService(fs, backend).RecycleAsync(@"D:\data", @"D:\data\file", default);
        Assert.Equal(RecycleStatus.Failed, result.Status); Assert.Empty(backend.Paths);
    }
    [Fact]
    public async Task ValidatedPathIsPassedWithoutCommandInterpolation()
    {
        var fs = new FakeFileSystem(); fs.Directory(@"D:\"); fs.Directory(@"D:\data"); fs.File(@"D:\data\Tiếng Việt ' file", 10);
        var backend = new RecordingBackend();
        var result = await new WindowsShellService(fs, backend).RecycleAsync(@"D:\data", @"D:\data\Tiếng Việt ' file", default);
        Assert.Equal(RecycleStatus.Success, result.Status); Assert.Equal(new[] { @"D:\data\Tiếng Việt ' file" }, backend.Paths);
    }
    private sealed class RecordingBackend : IRecycleBackend
    {
        public List<string> Paths { get; } = [];
        public RecycleResult Recycle(string path, CancellationToken token) { Paths.Add(path); return new(RecycleStatus.Success); }
    }
}
