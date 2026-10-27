using FolderSize.Core.Actions;
using Xunit;
namespace FolderSize.Tests;
public class PathPolicyTests
{
    private static FakeFileSystem Fixture()
    {
        var fs = new FakeFileSystem(); fs.Directory(@"D:\"); fs.Directory(@"D:\data"); fs.Directory(@"D:\data\sub");
        fs.File(@"D:\data\sub\file", 1); fs.File(@"D:\data\sub\Tiếng Việt ' file", 1); return fs;
    }
    [Theory]
    [InlineData(@"D:\data")][InlineData(@"D:\database\a")][InlineData(@"D:\other")][InlineData(@"D:\")]
    [InlineData(@"D:\data\missing")][InlineData(@"relative")][InlineData(@"D:\data\sub\file:stream")]
    public void RejectsRootOutsidePrefixAndMissing(string candidate) => Assert.False(PathPolicy.ValidateRecycle(@"D:\data", candidate, Fixture()).Allowed);

    [Fact]
    public void RejectsRootOutsidePrefixAndReparseAncestors()
    {
        var fs = Fixture(); fs.Entries[@"D:\data\sub"] = fs.Entries[@"D:\data\sub"] with { Attributes = FileAttributes.Directory | FileAttributes.ReparsePoint };
        Assert.False(PathPolicy.ValidateRecycle(@"D:\data", @"D:\data\sub\file", fs).Allowed);
        fs = Fixture(); fs.Entries[@"D:\data"] = fs.Entries[@"D:\data"] with { Attributes = FileAttributes.Directory | FileAttributes.ReparsePoint };
        Assert.False(PathPolicy.ValidateRecycle(@"D:\data", @"D:\data\sub\file", fs).Allowed);
    }
    [Theory]
    [InlineData(@"d:/DATA/sub/file")][InlineData(@"D:\data\sub\..\sub\file")]
    [InlineData(@"D:\data\sub\Tiếng Việt ' file")][InlineData(@"D:\data\sub\")]
    public void AcceptsNormalizedDescendants(string candidate) => Assert.True(PathPolicy.ValidateRecycle(@"D:\data\", candidate, Fixture()).Allowed);
}
