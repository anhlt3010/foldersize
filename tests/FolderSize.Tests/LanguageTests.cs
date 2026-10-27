using System.ComponentModel;
using FolderSize.App.Localization;
using FolderSize.App.ViewModels;
using FolderSize.Core.Actions;
using FolderSize.Core.Scanning;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace FolderSize.Tests;

public class LanguageTests
{
    [Fact]
    public async Task LanguageCanChangeWhileScanIsRunningWithoutRestartingIt()
    {
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        var root = Path.GetFullPath(".work/language-busy"); var file = Path.Combine(root, "Tệp.bin");
        var fs = new FakeFileSystem(); fs.Directory(root, file); fs.File(file, 1536); var rootReads = 0;
        fs.OnRead = path => { if (path == root) { Interlocked.Increment(ref rootReads); entered.Set(); release.Wait(TimeSpan.FromSeconds(5)); } };
        var text = new LanguageService();
        using var vm = new MainViewModel(new(fs), new FakeShell(), new FakeDialogs(), text) { PathInput = root };
        var scan = vm.ScanAsync();
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(3)));
            text.LanguageCode = "en"; Assert.Equal("Scanning…", vm.StateLabel); Assert.False(vm.CanScan); Assert.False(vm.CanRecycle);
        }
        finally { release.Set(); await scan; }
        Assert.Equal(1, rootReads); Assert.Equal("Scan complete", vm.StateLabel); Assert.Equal("1.5 KiB", vm.TotalSize);
    }
    [Fact]
    public void CatalogTranslationsPreserveFormattingArguments()
    {
        using var stream = typeof(LanguageService).Assembly.GetManifestResourceStream("FolderSize.App.Localization.Strings.json")!;
        using var catalog = JsonDocument.Parse(stream);
        var vi = new LanguageService(); var en = new LanguageService { LanguageCode = "en" };
        foreach (var item in catalog.RootElement.EnumerateObject())
        {
            Assert.False(string.IsNullOrWhiteSpace(vi[item.Name])); Assert.False(string.IsNullOrWhiteSpace(en[item.Name]));
            var viArguments = Regex.Matches(vi[item.Name], @"\{\d+(?::[^}]+)?\}").Select(match => match.Value).Order().ToArray();
            var enArguments = Regex.Matches(en[item.Name], @"\{\d+(?::[^}]+)?\}").Select(match => match.Value).Order().ToArray();
            Assert.Equal(viArguments, enArguments);
            Assert.NotEmpty(vi.Format(item.Name, 123, 456)); Assert.NotEmpty(en.Format(item.Name, 123, 456));
        }
        Assert.Equal("The Shell did not confirm recycling (0x80004005).", en.TranslateKnownMessage("Shell không xác nhận recycle (0x80004005)."));
    }

    [Fact]
    public void FailedPreferenceReplacementPreservesPreviousFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "FolderSize-language-" + Guid.NewGuid());
        var path = Path.Combine(directory, "settings.json");
        try
        {
            var store = new JsonLanguagePreferenceStore(path); Assert.True(store.TrySave("vi"));
            using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read)) Assert.False(store.TrySave("en"));
            Assert.Equal("vi", store.Load()); Assert.Single(Directory.GetFiles(directory));
        }
        finally { if (File.Exists(path)) File.Delete(path); if (Directory.Exists(directory)) Directory.Delete(directory); }
    }
    [Fact]
    public async Task SwitchingCompletedScanRetainsRowsSelectionAndReformatsNumbers()
    {
        var root = Path.GetFullPath(".work/Ngôn ngữ"); var sub = Path.Combine(root, "Thư mục"); var file = Path.Combine(sub, "Tệp tiếng Việt.bin");
        var fs = new FakeFileSystem(); fs.Directory(root, sub); fs.Directory(sub, file); fs.File(file, 1536);
        var text = new LanguageService();
        using var vm = new MainViewModel(new(fs), new FakeShell(), new FakeDialogs(), text) { PathInput = root };
        await vm.ScanAsync(); vm.ToggleExpanded(vm.Rows[1]); vm.SelectedRow = vm.Rows[2];
        var rows = vm.Rows.ToArray(); var selection = vm.SelectedRow;
        Assert.Equal("1,5 KiB", selection.Size);
        text.LanguageCode = "en";
        Assert.Equal("Scan complete", vm.StateLabel); Assert.StartsWith("Scan complete.", vm.StatusText);
        Assert.Equal("1.5 KiB", vm.TotalSize); Assert.Equal("1.5 KiB", selection.Size);
        Assert.Equal(rows, vm.Rows); Assert.Same(selection, vm.SelectedRow); Assert.Equal(file, selection.Node.FullPath);
        Assert.Equal("Tệp tiếng Việt.bin", selection.Name); Assert.True(vm.CanRecycle); Assert.True(vm.Rows[1].Row.IsExpanded);
        vm.SelectedRow = vm.Rows[0]; Assert.False(vm.CanRecycle); Assert.Contains("scan root", vm.RecycleReason);
        text.LanguageCode = "vi"; Assert.Equal("Quét hoàn tất", vm.StateLabel); Assert.Equal("1,5 KiB", vm.TotalSize);
    }

    [Fact]
    public async Task EnglishFailureCancelAndRecycleStatesStayLocalized()
    {
        var text = new LanguageService { LanguageCode = "en" }; var dialogs = new FakeDialogs();
        using var failed = new MainViewModel(new(new FakeFileSystem()), new FakeShell(), dialogs, text) { PathInput = "relative" };
        await failed.ScanAsync(); Assert.StartsWith("Unable to scan:", failed.StatusText);
        Assert.Equal("Please enter an absolute path.", Assert.Single(dialogs.Errors));
        var root = Path.GetFullPath(".work/language-cancel"); var file = Path.Combine(root, "file");
        var fs = new FakeFileSystem(); fs.Directory(root, file); fs.File(file, 10);
        using var vm = new MainViewModel(new(fs), new FakeShell { Result = new(RecycleStatus.Failed, "Không thể xóa gốc vùng quét.") }, dialogs, text) { PathInput = root };
        await vm.ScanAsync(); vm.SelectedRow = vm.Rows[1]; await vm.RecycleSelectedAsync();
        Assert.Equal("Rescan required", vm.StateLabel); Assert.Equal("Cannot recycle the scan root.", dialogs.Errors.Last());
        fs.OnRead = _ => vm.Cancel(); await vm.ScanAsync(); Assert.StartsWith("Canceled", vm.StatusText);
        text.LanguageCode = "vi"; Assert.StartsWith("Đã hủy", vm.StatusText);
    }
    [Fact]
    public void SwitchingLanguageUpdatesTextCultureAndPersistsOnce()
    {
        var store = new MemoryPreferences("vi"); var text = new LanguageService(store);
        var properties = new List<string?>(); var changes = 0;
        text.PropertyChanged += (_, e) => properties.Add(e.PropertyName);
        text.LanguageChanged += (_, _) => changes++;
        Assert.Equal("Quét / Quét lại", text["ScanButton"]);
        text.LanguageCode = "en";
        Assert.Equal("Scan / Rescan", text["ScanButton"]);
        Assert.Equal("12,345", 12345.ToString("N0", text.Culture));
        Assert.Equal("en", store.Saved); Assert.Equal(1, changes); Assert.Contains("Item[]", properties);
        text.LanguageCode = "en"; Assert.Equal(1, store.SaveCount); Assert.Equal(1, changes);
        Assert.Equal("Scan / Rescan", new LanguageService(store)["ScanButton"]);
    }

    [Fact]
    public void SaveFailureKeepsNewLanguageAndReportsFailure()
    {
        var store = new MemoryPreferences("vi") { Writable = false };
        var text = new LanguageService(store); var failures = 0; text.SaveFailed += (_, _) => failures++;
        text.LanguageCode = "en";
        Assert.Equal("en", text.LanguageCode); Assert.Equal("Cancel", text["Cancel"]); Assert.Equal(1, failures);
    }

    [Theory]
    [InlineData(null)][InlineData("zh")][InlineData("")]
    public void UnknownOrMissingPreferenceFallsBackToVietnamese(string? code)
    {
        var text = new LanguageService(new MemoryPreferences(code));
        Assert.Equal("vi", text.LanguageCode); Assert.Equal("Hủy", text["Cancel"]);
    }

    [Fact]
    public void JsonPreferenceSurvivesNewInstanceAndCorruptFileFallsBack()
    {
        var directory = Path.Combine(Path.GetTempPath(), "FolderSize-language-" + Guid.NewGuid());
        var path = Path.Combine(directory, "settings.json");
        try
        {
            var store = new JsonLanguagePreferenceStore(path);
            Assert.Null(store.Load()); Assert.True(store.TrySave("en"));
            Assert.Equal("en", new JsonLanguagePreferenceStore(path).Load());
            Assert.Equal("en", new LanguageService(new JsonLanguagePreferenceStore(path)).LanguageCode);
            File.WriteAllText(path, "{broken");
            Assert.Equal("vi", new LanguageService(new JsonLanguagePreferenceStore(path)).LanguageCode);
            File.WriteAllText(path, "{\"language\":123}"); Assert.Null(store.Load());
        }
        finally { if (File.Exists(path)) File.Delete(path); if (Directory.Exists(directory)) Directory.Delete(directory); }
    }

    [Fact]
    public void KnownMessagesTranslateButPathsAndNativeDetailsStayUnchanged()
    {
        var text = new LanguageService { LanguageCode = "en" };
        Assert.Equal("Cannot recycle the scan root.", text.TranslateKnownMessage("Không thể xóa gốc vùng quét."));
        Assert.Equal(@"C:\Thư mục\Tệp tiếng Việt.bin", text.TranslateKnownMessage(@"C:\Thư mục\Tệp tiếng Việt.bin"));
        Assert.Equal("Native OS detail", text.TranslateKnownMessage("Native OS detail"));
    }
}

internal sealed class MemoryPreferences(string? value) : ILanguagePreferenceStore
{
    public string? Saved { get; private set; } = value;
    public bool Writable { get; set; } = true;
    public int SaveCount { get; private set; }
    public string? Load() => Saved;
    public bool TrySave(string languageCode) { SaveCount++; if (!Writable) return false; Saved = languageCode; return true; }
}
