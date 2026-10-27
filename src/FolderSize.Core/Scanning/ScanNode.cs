namespace FolderSize.Core.Scanning;

public sealed class ScanNode(string fullPath, string name, bool isDirectory)
{
    public string FullPath { get; } = fullPath;
    public string Name { get; } = name;
    public bool IsDirectory { get; } = isDirectory;
    public long SizeBytes { get; internal set; }
    public long FileCount { get; internal set; }
    public long DirectoryCount { get; internal set; }
    public bool IsIncomplete { get; internal set; }
    public bool IsSkipped { get; internal set; }
    public List<ScanNode> Children { get; } = [];
}
public sealed record ScanIssue(string Path, string Reason);
public sealed record ScanResult(ScanNode Root, bool IsCanceled, IReadOnlyList<ScanIssue> Issues);
public sealed record ScanProgress(string CurrentPath, long SizeBytes, long FileCount, long DirectoryCount, ScanNode Snapshot);
