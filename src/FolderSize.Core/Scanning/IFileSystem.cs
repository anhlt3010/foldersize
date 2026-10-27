namespace FolderSize.Core.Scanning;

public sealed record FileEntry(string FullPath, string Name, bool IsDirectory, FileAttributes Attributes, long Length);
public interface IFileSystem
{
    FileEntry ReadEntry(string path);
    IEnumerable<string> Enumerate(string directory);
}
