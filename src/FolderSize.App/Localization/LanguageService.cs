using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FolderSize.App.Localization;

public sealed class LanguageService : INotifyPropertyChanged
{
    private sealed record Translation(string Vi, string En);
    private static readonly Dictionary<string, Translation> Catalog = LoadCatalog();
    private static readonly Dictionary<string, string> KnownMessages = Catalog
        .SelectMany(pair => new[] { (Text: pair.Value.Vi, Key: pair.Key), (Text: pair.Value.En, Key: pair.Key) })
        .GroupBy(item => item.Text, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First().Key, StringComparer.Ordinal);
    public static LanguageService Current { get; } = new();
    public static IReadOnlyList<LanguageOption> AvailableLanguages { get; } = [new("vi", "Tiếng Việt"), new("en", "English")];
    private ILanguagePreferenceStore? store;
    private string languageCode = "vi";
    public LanguageService(ILanguagePreferenceStore? store = null) { if (store is not null) LoadPreferences(store); }
    public string LanguageCode
    {
        get => languageCode;
        set
        {
            if (value is not ("vi" or "en")) throw new ArgumentOutOfRangeException(nameof(value));
            if (languageCode == value) return;
            Apply(value);
            if (store is not null && !store.TrySave(value)) SaveFailed?.Invoke(this, EventArgs.Empty);
        }
    }
    public CultureInfo Culture => CultureInfo.GetCultureInfo(languageCode == "en" ? "en-US" : "vi-VN");
    public string this[string key] => Catalog.TryGetValue(key, out var value) ? languageCode == "en" ? value.En : value.Vi : key;
    public string Format(string key, params object?[] args) => string.Format(Culture, this[key], args);
    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? LanguageChanged;
    public event EventHandler? SaveFailed;
    public void LoadPreferences(ILanguagePreferenceStore preferences)
    {
        store = preferences;
        Apply(preferences.Load() == "en" ? "en" : "vi");
    }
    public string TranslateKnownMessage(string message)
    {
        if (KnownMessages.TryGetValue(message, out var key)) return this[key];
        // The Shell's HRESULT is a dynamic argument; keep the code unchanged.
        foreach (var template in new[] { Catalog["ShellNoConfirm"].Vi, Catalog["ShellNoConfirm"].En })
        {
            var match = Regex.Match(message, "^" + Regex.Escape(template).Replace("\\{0}", "(.*?)") + "$", RegexOptions.CultureInvariant);
            if (match.Success) return Format("ShellNoConfirm", match.Groups[1].Value);
        }
        return message; // OS-localized errors and filesystem paths are not translated.
    }
    private void Apply(string code)
    {
        if (languageCode == code) return;
        languageCode = code;
        PropertyChanged?.Invoke(this, new(nameof(LanguageCode)));
        PropertyChanged?.Invoke(this, new(nameof(Culture)));
        PropertyChanged?.Invoke(this, new("Item[]"));
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }
    private static Dictionary<string, Translation> LoadCatalog()
    {
        using var stream = typeof(LanguageService).Assembly.GetManifestResourceStream("FolderSize.App.Localization.Strings.json")
            ?? throw new InvalidOperationException("Missing language catalog.");
        return JsonSerializer.Deserialize<Dictionary<string, Translation>>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Invalid language catalog.");
    }
}
public sealed record LanguageOption(string Code, string Name);
