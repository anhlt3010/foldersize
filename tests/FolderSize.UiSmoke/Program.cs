using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FolderSize.App;
using FolderSize.App.ViewModels;
using FolderSize.App.Services;
using FolderSize.Core.Actions;
using FolderSize.Core.Scanning;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var exit = 1;
        var app = new Application();
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/FolderSize.App;component/Themes/Styles.xaml", UriKind.Relative) });
        app.Startup += async (_, _) =>
        {
            MainWindow? window = null;
            try
            {
                var output = Path.GetFullPath(args.FirstOrDefault() ?? "artifacts/ui"); Directory.CreateDirectory(output);
                window = new MainWindow { Left = -20000, Top = -20000, ShowActivated = false };
                window.Show(); await Layout(window); Save(window, Path.Combine(output, "empty.png"));
                if (window.Icon is null) throw new Exception("Application icon missing");
                using (var iconStream = File.OpenRead("src/FolderSize.App/Assets/FolderSize.ico"))
                {
                    var decoder = new IconBitmapDecoder(iconStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    if (decoder.Frames.Count != 9 || !decoder.Frames.Any(frame => frame.PixelWidth == 16) || !decoder.Frames.Any(frame => frame.PixelWidth == 256))
                        throw new Exception("Multi-resolution application icon invalid");
                }
                var about = new AboutWindow { Owner = window, WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000, ShowActivated = false };
                about.Show(); await Layout(about); Save(about, Path.Combine(output, "about.png")); about.Close();
                var vm = new MainViewModel(new(new SyntheticFileSystem()), new NoShell(), new NoDialogs()) { PathInput = @"C:\FolderSizeFixture" };
                window.DataContext = vm;
                await vm.ScanAsync(); await Layout(window);
                if (vm.Rows.Count != 2) throw new Exception("Closed branch should have only two rows");
                vm.SelectedRow = vm.Rows[1]; vm.ToggleExpanded(vm.Rows[1]); await Layout(window);
                var list = (ListView)window.FindName("TreeList");
                var count = Descendants<ListViewItem>(list).Count();
                Console.WriteLine($"100,000 files: {vm.Rows.Count} visible rows, {count} realized containers; DPI={VisualTreeHelper.GetDpi(window).PixelsPerDip}");
                if (vm.Rows.Count != 100002 || count < 1 || count > 150) throw new Exception("Virtualization failed");
                if (Descendants<Image>(list).Count(image => image.Source is not null) != count) throw new Exception("Windows tree icons missing");
                if (vm.SelectedRow?.Node.Name != "Thư mục lớn") throw new Exception("Selection lost");
                Save(window, Path.Combine(output, "expanded.png"));
                vm.ToggleExpanded(vm.Rows[1]); await Layout(window);
                if (vm.Rows.Count != 2) throw new Exception("Collapse retained hidden rows");
                window.Width = 900; window.Height = 600; await Layout(window); Save(window, Path.Combine(output, "minimum.png"));
                // Exercise 150% layout without altering the user's Windows DPI settings.
                ((FrameworkElement)window.Content).LayoutTransform = new ScaleTransform(1.5, 1.5);
                window.Width = 1350; window.Height = 900; await Layout(window); Save(window, Path.Combine(output, "layout-150.png"));
                Console.WriteLine("PASS: expand/collapse, retained selection, bounded containers, minimum and 150% layout renders."); exit = 0;
            }
            catch (Exception e) { Console.WriteLine(e); }
            finally { window?.Close(); app.Shutdown(); }
        };
        app.Run(); return exit;
    }
    private static async Task Layout(Window window)
    { await window.Dispatcher.InvokeAsync(() => { window.UpdateLayout(); }, DispatcherPriority.ApplicationIdle); }
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        { var child = VisualTreeHelper.GetChild(root, i); if (child is T match) yield return match; foreach (var nested in Descendants<T>(child)) yield return nested; }
    }
    private static void Save(Window window, string file)
    {
        FrameworkElement content = window;
        var bitmap = new RenderTargetBitmap((int)content.ActualWidth, (int)content.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(content);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(file); encoder.Save(stream);
    }
    private sealed class SyntheticFileSystem : IFileSystem
    {
        private const string Root = @"C:\FolderSizeFixture";
        private const string Sub = Root + @"\Thư mục lớn";
        public FileEntry ReadEntry(string path) => path == Root || path == Sub
            ? new(path, Path.GetFileName(path), true, FileAttributes.Directory, 0)
            : new(path, Path.GetFileName(path), false, FileAttributes.Normal, 1024);
        public IEnumerable<string> Enumerate(string directory) => directory == Root ? [Sub] : Enumerable.Range(0, 100000).Select(i => Sub + $"\\Tệp tiếng Việt {i:D6}.bin");
    }
    private sealed class NoShell : IShellService
    { public Task OpenInExplorerAsync(string path) => throw new InvalidOperationException(); public Task<RecycleResult> RecycleAsync(string root, string path, CancellationToken token) => throw new InvalidOperationException(); }
    private sealed class NoDialogs : IUserDialogs
    { public Task<bool> ConfirmRecycleAsync(string path, long scannedBytes) => Task.FromResult(false); public void ShowError(string title, string message) => throw new Exception(message); }
}
