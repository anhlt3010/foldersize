using System.Windows.Data;
using System.Windows.Markup;

namespace FolderSize.App.Localization;

public sealed class TextExtension(string key) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) => new Binding($"[{key}]")
    { Source = LanguageService.Current, Mode = BindingMode.OneWay }.ProvideValue(serviceProvider);
}
