using FolderSize.Core.Scanning;

namespace FolderSize.Core.Presentation;

public sealed record NodeRow(ScanNode Node, int Depth, bool HasChildren, bool IsExpanded, double? ParentShare);
public static class TreeProjection
{
    public static IReadOnlyList<NodeRow> Flatten(ScanNode root, ISet<string> expandedPaths, bool sortBySize)
    {
        var rows = new List<NodeRow>();
        var stack = new Stack<(ScanNode Node, int Depth, long? ParentSize)>();
        stack.Push((root, 0, null));
        while (stack.TryPop(out var item))
        {
            var expanded = expandedPaths.Contains(item.Node.FullPath);
            var share = item.ParentSize > 0 ? (double?)(100d * item.Node.SizeBytes / item.ParentSize.Value) : null;
            rows.Add(new(item.Node, item.Depth, item.Node.Children.Count > 0, expanded, share));
            if (!expanded) continue;
            var children = sortBySize ? item.Node.Children.OrderByDescending(x => x.SizeBytes)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Name, StringComparer.Ordinal).ToArray()
                : item.Node.Children.ToArray();
            for (var i = children.Length - 1; i >= 0; i--)
                stack.Push((children[i], item.Depth + 1, item.Node.SizeBytes));
        }
        return rows;
    }
}
