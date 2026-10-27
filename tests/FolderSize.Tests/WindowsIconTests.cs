using FolderSize.App.Services;
using Xunit;

namespace FolderSize.Tests;

public class WindowsIconTests
{
    [Fact]
    public void WindowsFolderAndDriveIconsAreFrozenAndReusable()
    {
        RunSta(() =>
        {
            var icons = new WindowsIconProvider();
            var closed = icons.GetIcon(@"C:\nonexistent-folder", true, false);
            var open = icons.GetIcon(@"C:\nonexistent-folder", true, true);
            var drive = icons.GetIcon(@"C:\", true, true);
            Assert.NotNull(closed); Assert.True(closed.IsFrozen);
            Assert.NotNull(open); Assert.True(open.IsFrozen);
            Assert.NotNull(drive); Assert.True(drive.IsFrozen);
            Assert.NotSame(closed, open); Assert.NotSame(closed, drive);
            Assert.Same(closed, icons.GetIcon(@"Z:\another-missing-folder", true, false));
        });
    }

    [Fact]
    public void FileIconsUseTypeAssociationWithoutReadingTargetFiles()
    {
        RunSta(() =>
        {
            var icons = new WindowsIconProvider();
            var text = icons.GetIcon(@"Z:\does-not-exist\Tiếng Việt ' data.txt", false, false);
            Assert.NotNull(text); Assert.True(text.IsFrozen);
            Assert.Same(text, icons.GetIcon(@"C:\another-nonexistent.TXT", false, false));
            var unknown = icons.GetIcon(@"Z:\does-not-exist\unknown.foldersize-test-unknown", false, false);
            Assert.NotNull(unknown); Assert.True(unknown.IsFrozen);
        });
    }

    private static void RunSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() => { try { action(); } catch (Exception e) { error = e; } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (error is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }
}
