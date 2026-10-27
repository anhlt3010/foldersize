using FolderSize.Core.Presentation;
using FolderSize.Core.Scanning;
using FolderSize.App.Infrastructure;
namespace FolderSize.App.ViewModels;
public sealed class NodeRowViewModel(NodeRow row) : ObservableObject
{
    public ScanNode Node => row.Node;
    public NodeRow Row => row;
    public string Name => Node.Name;
    public string Size => SizeFormatter.Format(Node.SizeBytes);
    public string Count => Node.FileCount.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN"));
    public string Share => Row.ParentShare is double value ? value.ToString("0.#", System.Globalization.CultureInfo.GetCultureInfo("vi-VN")) + "%" : "—";
    public double ShareValue => Row.ParentShare ?? 0;
    public string Toggle => !Row.HasChildren ? "" : Row.IsExpanded ? "⌄" : "›";
    public System.Windows.Thickness Indent => new(Row.Depth * 20, 0, 0, 0);
    public string Note => Node.IsSkipped ? "Bỏ qua liên kết" : Node.IsIncomplete ? "Chưa đầy đủ" : "";
    public void Update(NodeRow value)
    { row = value; foreach (var name in new[] { nameof(Node), nameof(Row), nameof(Name), nameof(Size), nameof(Count), nameof(Share), nameof(ShareValue), nameof(Toggle), nameof(Indent), nameof(Note) }) Notify(name); }
}
