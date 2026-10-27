using System.IO;
using System.Security;
using System.Text.Json;

namespace FolderSize.App.Localization;

public interface ILanguagePreferenceStore
{
    string? Load();
    bool TrySave(string languageCode);
}
public sealed class JsonLanguagePreferenceStore(string path) : ILanguagePreferenceStore
{
    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FolderSize", "settings.json");
    public string? Load()
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("language", out var language)
                && language.ValueKind == JsonValueKind.String ? language.GetString() : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or SecurityException or JsonException) { return null; }
    }
    public bool TrySave(string languageCode)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(temporary, JsonSerializer.Serialize(new { language = languageCode }));
            File.Move(temporary, path, true);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or SecurityException) { return false; }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or SecurityException) { }
        }
    }
}
