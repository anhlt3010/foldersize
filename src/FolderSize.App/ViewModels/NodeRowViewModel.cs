using FolderSize.Core.Presentation;
using FolderSize.Core.Scanning;
using FolderSize.App.Infrastructure;
namespace FolderSize.App.ViewModels;
public sealed class NodeRowViewModel(NodeRow row) : ObservableObject
{
    public ScanNode Node => row.Node;
    public NodeRow Row => row;
    public void Update(NodeRow value) { row = value; Notify(nameof(Node)); Notify(nameof(Row)); }
}
