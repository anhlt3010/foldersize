namespace FolderSize.Core.Scanning;

public sealed class DirectoryScanner(IFileSystem fs)
{
    public Task<ScanResult> ScanAsync(string root, IProgress<ScanProgress>? progress, CancellationToken token)
        => Task.Run(() => Scan(root, progress, token));

    private ScanResult Scan(string path, IProgress<ScanProgress>? progress, CancellationToken token)
    {
        var entry = fs.ReadEntry(path);
        if (!entry.IsDirectory) throw new ArgumentException("Vùng quét phải là thư mục hoặc ổ đĩa.");
        var root = Create(entry);
        var issues = new List<ScanIssue>();
        var stack = new Stack<Frame>();
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var currentPath = path;
        if (root.IsSkipped) Issue(root, path, "Bỏ qua liên kết hoặc reparse point.");
        else Enter(root);
        while (stack.TryPeek(out var frame))
        {
            if (token.IsCancellationRequested) break;
            if (progress is not null && timer.ElapsedMilliseconds >= 500)
            { Report(); timer.Restart(); }
            bool hasNext;
            try { hasNext = frame.Entries.MoveNext(); }
            catch (Exception e) when (IsFileError(e)) { Issue(frame.Node, frame.Node.FullPath, e.Message); hasNext = false; }
            if (!hasNext)
            {
                frame.Entries.Dispose();
                stack.Pop();
                if (stack.TryPeek(out var parent)) Add(parent.Node, frame.Node);
                continue;
            }
            ScanNode child;
            currentPath = frame.Entries.Current;
            try { child = Create(fs.ReadEntry(frame.Entries.Current)); }
            catch (Exception e) when (IsFileError(e)) { Issue(frame.Node, frame.Entries.Current, e.Message); continue; }
            frame.Node.Children.Add(child);
            if (child.IsSkipped) { Issue(child, child.FullPath, "Bỏ qua liên kết hoặc reparse point."); Add(frame.Node, child); }
            else if (child.IsDirectory) { if (!Enter(child)) Add(frame.Node, child); }
            else Add(frame.Node, child);
        }
        var canceled = token.IsCancellationRequested;
        while (stack.TryPop(out var unfinished))
        {
            unfinished.Entries.Dispose(); unfinished.Node.IsIncomplete = true;
            if (stack.TryPeek(out var parent)) Add(parent.Node, unfinished.Node);
        }
        if (canceled) root.IsIncomplete = true;
        if (progress is not null) Report();
        return new(root, canceled, issues);

        void Report()
        {
            var snapshot = Snapshot(root);
            progress!.Report(new(currentPath, snapshot.SizeBytes, snapshot.FileCount, snapshot.DirectoryCount, snapshot));
        }

        bool Enter(ScanNode node)
        {
            try { stack.Push(new(node, fs.Enumerate(node.FullPath).GetEnumerator())); return true; }
            catch (Exception e) when (IsFileError(e)) { Issue(node, node.FullPath, e.Message); return false; }
        }
        void Issue(ScanNode node, string location, string reason)
        { node.IsIncomplete = true; issues.Add(new(location, reason)); }
    }

    private static bool IsFileError(Exception e) => e is IOException or UnauthorizedAccessException or System.Security.SecurityException;

    private static ScanNode Create(FileEntry entry) => new(entry.FullPath, entry.Name, entry.IsDirectory)
    {
        SizeBytes = entry.IsDirectory || entry.Attributes.HasFlag(FileAttributes.ReparsePoint) ? 0 : entry.Length,
        FileCount = entry.IsDirectory ? 0 : 1,
        IsSkipped = entry.Attributes.HasFlag(FileAttributes.ReparsePoint)
    };

    private static ScanNode Snapshot(ScanNode root)
    {
        ScanNode Copy(ScanNode source) => new(source.FullPath, source.Name, source.IsDirectory)
        { IsSkipped = source.IsSkipped, IsIncomplete = source.IsIncomplete, SizeBytes = source.IsDirectory ? 0 : source.SizeBytes, FileCount = source.IsDirectory ? 0 : source.FileCount };
        var result = Copy(root);
        var stack = new Stack<(ScanNode Source, ScanNode Target, bool Finish)>();
        stack.Push((root, result, false));
        while (stack.TryPop(out var item))
        {
            if (item.Finish) { foreach (var child in item.Target.Children) Add(item.Target, child); continue; }
            stack.Push((item.Source, item.Target, true));
            foreach (var child in item.Source.Children)
            {
                var copy = Copy(child); item.Target.Children.Add(copy);
                stack.Push((child, copy, false));
            }
        }
        return result;
    }

    private static void Add(ScanNode parent, ScanNode child)
    {
        parent.SizeBytes += child.SizeBytes;
        parent.FileCount += child.FileCount;
        parent.DirectoryCount += child.DirectoryCount + (child.IsDirectory ? 1 : 0);
        parent.IsIncomplete |= child.IsIncomplete;
    }
    private sealed record Frame(ScanNode Node, IEnumerator<string> Entries);
}
