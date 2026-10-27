using FolderSize.Core.Presentation;
using FolderSize.Core.Scanning;
using FolderSize.App.Infrastructure;
using FolderSize.App.Localization;
namespace FolderSize.App.ViewModels;
public sealed class NodeRowViewModel(NodeRow row, LanguageService? languageService = null) : ObservableObject
{
    private readonly LanguageService language = languageService ?? new();
    public ScanNode Node => row.Node;
    public NodeRow Row => row;
    public string Name => Node.Name;
    public string Size => SizeFormatter.Format(Node.SizeBytes, language.Culture);
    public string Count => Node.FileCount.ToString("N0", language.Culture);
    public string Share => Row.ParentShare is double value ? value.ToString("0.#", language.Culture) + "%" : "—";
    public double ShareValue => Row.ParentShare ?? 0;
    public System.Windows.Thickness Indent => new(Row.Depth * 20, 0, 0, 0);
    public string Note => Node.IsSkipped ? language["NoteSkipped"] : Node.IsIncomplete ? language["NoteIncomplete"] : "";
    public void RefreshLocalized()
    { foreach (var name in new[] { nameof(Size), nameof(Count), nameof(Share), nameof(Note) }) Notify(name); }
    public void Update(NodeRow value)
    { row = value; foreach (var name in new[] { nameof(Node), nameof(Row), nameof(Name), nameof(Size), nameof(Count), nameof(Share), nameof(ShareValue), nameof(Indent), nameof(Note) }) Notify(name); }
}
