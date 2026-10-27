namespace FolderSize.Core.Scanning;

public sealed class PhysicalFileSystem : IFileSystem
{
    public FileEntry ReadEntry(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var attributes = File.GetAttributes(fullPath);
        var isDirectory = attributes.HasFlag(FileAttributes.Directory);
        // Metadata only. Never open a stream, including for cloud placeholders.
        var length = isDirectory || attributes.HasFlag(FileAttributes.ReparsePoint) ? 0 : new FileInfo(fullPath).Length;
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(fullPath));
        return new(fullPath, string.IsNullOrEmpty(name) ? fullPath : name, isDirectory, attributes, length);
    }
    public IEnumerable<string> Enumerate(string directory) => Directory.EnumerateFileSystemEntries(directory);
}
