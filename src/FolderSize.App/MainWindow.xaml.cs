using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FolderSize.App.Services;
using FolderSize.App.ViewModels;
using FolderSize.Core.Scanning;
namespace FolderSize.App;
public partial class MainWindow : Window
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel(new DirectoryScanner(new PhysicalFileSystem()), new WindowsShellService(), new UserDialogs());
    }
    private void Pick_Click(object sender, RoutedEventArgs e) { var path = FolderPicker.Pick(); if (path is not null) ViewModel.PathInput = path; }
    private void About_Click(object sender, RoutedEventArgs e) => new AboutWindow { Owner = this }.ShowDialog();
    private void Tree_KeyDown(object sender, KeyEventArgs e)
    {
        if (ViewModel.SelectedRow is not { } row) return;
        if ((e.Key == Key.Right && !row.Row.IsExpanded || e.Key == Key.Left && row.Row.IsExpanded) && row.Row.HasChildren)
        { ViewModel.ToggleExpanded(row); e.Handled = true; }
    }
    private void Help_Click(object sender, RoutedEventArgs e) => MessageBox.Show("1. Chọn thư mục hoặc nhập đường dẫn tuyệt đối.\n2. Quét, rồi mở các nhánh bằng mũi tên hoặc phím Trái/Phải.\n3. Chọn một mục để mở Explorer hoặc chuyển vào Thùng rác.\n\nKích thước logic dùng hệ 1024 (KiB/MiB/GiB), có thể khác dung lượng thực trên ổ. Hard link được tính theo mỗi đường dẫn. Liên kết và file reparse bị bỏ qua, không tải nội dung file đám mây. Lỗi đọc được ghi trong chi tiết.\n\nKết quả là ảnh chụp tại thời điểm quét. Chuyển vào Thùng rác chưa giải phóng chỗ; bạn tự quản lý Thùng rác trong Windows.", "Hướng dẫn FolderSize", MessageBoxButton.OK, MessageBoxImage.Information);
    private void Issues_Click(object sender, RoutedEventArgs e)
    {
        var list = new ListView { ItemsSource = ViewModel.Issues, Margin = new Thickness(16) };
        VirtualizingPanel.SetIsVirtualizing(list, true); VirtualizingPanel.SetVirtualizationMode(list, VirtualizationMode.Recycling); ScrollViewer.SetCanContentScroll(list, true);
        var view = new GridView(); view.Columns.Add(new GridViewColumn { Header = "Đường dẫn", Width = 440, DisplayMemberBinding = new System.Windows.Data.Binding("Path") }); view.Columns.Add(new GridViewColumn { Header = "Lý do", Width = 300, DisplayMemberBinding = new System.Windows.Data.Binding("Reason") }); list.View = view;
        new Window { Owner = this, Title = $"Chi tiết · {ViewModel.Issues.Count} mục", Width = 820, Height = 480, Content = list, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
    }
}
