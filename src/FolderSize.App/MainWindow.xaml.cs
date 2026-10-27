using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FolderSize.App.Services;
using FolderSize.App.ViewModels;
using FolderSize.Core.Scanning;
using FolderSize.App.Localization;
namespace FolderSize.App;
public partial class MainWindow : Window
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, e) => { if (e.OldValue is IDisposable old) old.Dispose(); };
        DataContext = new MainViewModel(new DirectoryScanner(new PhysicalFileSystem()), new WindowsShellService(), new UserDialogs(), LanguageService.Current);
        LanguageService.Current.SaveFailed += LanguageSaveFailed;
        Closed += (_, _) => { ViewModel.Dispose(); LanguageService.Current.SaveFailed -= LanguageSaveFailed; };
    }
    private void Pick_Click(object sender, RoutedEventArgs e) { var path = FolderPicker.Pick(); if (path is not null) ViewModel.PathInput = path; }
    private void About_Click(object sender, RoutedEventArgs e) => new AboutWindow { Owner = this }.ShowDialog();
    private void LanguageSaveFailed(object? sender, EventArgs e) => MessageBox.Show(this, LanguageService.Current["SaveLanguageFailed"], LanguageService.Current["SaveLanguageTitle"], MessageBoxButton.OK, MessageBoxImage.Warning);
    private void Tree_KeyDown(object sender, KeyEventArgs e)
    {
        if (ViewModel.SelectedRow is not { } row) return;
        if ((e.Key == Key.Right && !row.Row.IsExpanded || e.Key == Key.Left && row.Row.IsExpanded) && row.Row.HasChildren)
        { ViewModel.ToggleExpanded(row); e.Handled = true; }
    }
    private void Help_Click(object sender, RoutedEventArgs e) => MessageBox.Show(this, LanguageService.Current["HelpBody"], LanguageService.Current["HelpTitle"], MessageBoxButton.OK, MessageBoxImage.Information);
    private void Issues_Click(object sender, RoutedEventArgs e)
    {
        var language = LanguageService.Current;
        var list = new ListView { ItemsSource = ViewModel.Issues.Select(issue => new { issue.Path, Reason = language.TranslateKnownMessage(issue.Reason) }).ToArray(), Margin = new Thickness(16) };
        VirtualizingPanel.SetIsVirtualizing(list, true); VirtualizingPanel.SetVirtualizationMode(list, VirtualizationMode.Recycling); ScrollViewer.SetCanContentScroll(list, true);
        var view = new GridView(); view.Columns.Add(new GridViewColumn { Header = language["Path"], Width = 440, DisplayMemberBinding = new System.Windows.Data.Binding("Path") }); view.Columns.Add(new GridViewColumn { Header = language["Reason"], Width = 300, DisplayMemberBinding = new System.Windows.Data.Binding("Reason") }); list.View = view;
        new Window { Owner = this, Title = language.Format("IssuesTitle", ViewModel.Issues.Count), Width = 820, Height = 480, Content = list, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
    }
}
