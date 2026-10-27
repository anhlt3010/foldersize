using FolderSize.Core.Scanning;
namespace FolderSize.Core.Actions;
public sealed record PathValidation(bool Allowed, string Reason);
public static class PathPolicy
{
    public static PathValidation ValidateRecycle(string root, string candidate, IFileSystem fs)
    {
        try
        {
            var normalizedRoot = Normalize(root);
            var normalizedCandidate = Normalize(candidate);
            if (string.Equals(normalizedRoot, normalizedCandidate, StringComparison.OrdinalIgnoreCase)) return new(false, "Không thể xóa gốc vùng quét.");
            if (string.Equals(Path.GetPathRoot(normalizedCandidate), normalizedCandidate, StringComparison.OrdinalIgnoreCase)) return new(false, "Không thể xóa gốc ổ đĩa.");
            var prefix = Path.EndsInDirectorySeparator(normalizedRoot) ? normalizedRoot : normalizedRoot + Path.DirectorySeparatorChar;
            if (!normalizedCandidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return new(false, "Mục nằm ngoài vùng quét.");
            // Walk from the volume/share root, including ancestors of the scan root.
            var current = Path.GetPathRoot(normalizedCandidate)!;
            Check(current);
            foreach (var component in normalizedCandidate[current.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
            { current = Path.Combine(current, component); Check(current); }
            return new(true, "");
            void Check(string path)
            { if (fs.ReadEntry(path).Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new IOException("Đường dẫn chứa liên kết hoặc reparse point."); }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Security.SecurityException)
        { return new(false, e.Message); }
    }
    private static string Normalize(string path)
    {
        if (!Path.IsPathFullyQualified(path) || path.StartsWith(@"\\?\") || path.StartsWith(@"\\.\")) throw new ArgumentException("Đường dẫn không được hỗ trợ.");
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var relative = full[Path.GetPathRoot(full)!.Length..];
        if (relative.IndexOfAny([':', '*', '?', '"', '<', '>', '|']) >= 0 || relative.Split('\\').Any(x => x.EndsWith(' ') || x.EndsWith('.')))
            throw new ArgumentException("Đường dẫn chứa thành phần không an toàn.");
        return full;
    }
}
