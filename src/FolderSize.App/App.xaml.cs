using System.Windows;
using FolderSize.App.Localization;
namespace FolderSize.App;
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        LanguageService.Current.LoadPreferences(new JsonLanguagePreferenceStore(JsonLanguagePreferenceStore.DefaultPath));
        base.OnStartup(e);
    }
}
