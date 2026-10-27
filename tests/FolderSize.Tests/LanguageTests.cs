using System.ComponentModel;
using FolderSize.App.Localization;
using Xunit;

namespace FolderSize.Tests;

public class LanguageTests
{
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
