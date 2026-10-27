using System.Windows;

namespace FolderSize.App;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        VersionLabel.Text = "Phiên bản " + typeof(App).Assembly.GetName().Version?.ToString(3) + " · Windows x64";
    }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
