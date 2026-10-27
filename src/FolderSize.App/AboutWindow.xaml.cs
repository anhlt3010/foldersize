using System.Windows;
using FolderSize.App.Localization;

namespace FolderSize.App;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        UpdateVersion();
        LanguageService.Current.LanguageChanged += LanguageChanged;
        Closed += (_, _) => LanguageService.Current.LanguageChanged -= LanguageChanged;
    }
    private void LanguageChanged(object? sender, EventArgs e) => UpdateVersion();
    private void UpdateVersion() => VersionLabel.Text = LanguageService.Current.Format("VersionLabel", typeof(App).Assembly.GetName().Version?.ToString(3));
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
