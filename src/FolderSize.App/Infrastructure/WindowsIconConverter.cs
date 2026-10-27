using System.Globalization;
using System.Windows;
using System.Windows.Data;
using FolderSize.App.Services;

namespace FolderSize.App.Infrastructure;

public sealed class WindowsIconConverter : IMultiValueConverter
{
    private readonly WindowsIconProvider icons = new();
    public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        => values.Length == 3 && values[0] is string path && values[1] is bool directory && values[2] is bool expanded
            ? icons.GetIcon(path, directory, expanded) : DependencyProperty.UnsetValue;
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
