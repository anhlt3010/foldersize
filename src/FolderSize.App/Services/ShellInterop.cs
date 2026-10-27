using System.IO;
using System.Runtime.InteropServices;
using FolderSize.Core.Actions;
namespace FolderSize.App.Services;

internal static class ShellInterop
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    internal static extern void SHCreateItemFromParsingName(string path, IntPtr context, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IShellItem item);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHParseDisplayName(string name, IntPtr context, out IntPtr pidl, uint attributes, out uint result);
    [DllImport("shell32.dll", PreserveSig = false)]
    private static extern void SHOpenFolderAndSelectItems(IntPtr folder, uint count, IntPtr[] children, uint flags);
    [DllImport("shell32.dll")] private static extern IntPtr ILFindLastID(IntPtr pidl);
    internal static void SelectInExplorer(string path)
    {
        IntPtr item = IntPtr.Zero, parent = IntPtr.Zero;
        try
        {
            SHParseDisplayName(path, IntPtr.Zero, out item, 0, out _);
            if (string.Equals(Path.GetPathRoot(path), path, StringComparison.OrdinalIgnoreCase))
                SHOpenFolderAndSelectItems(item, 0, [], 0);
            else
            {
                var parentPath = Path.GetDirectoryName(path) ?? throw new IOException("Không xác định được thư mục cha.");
                SHParseDisplayName(parentPath, IntPtr.Zero, out parent, 0, out _);
                SHOpenFolderAndSelectItems(parent, 1, [ILFindLastID(item)], 0);
            }
        }
        finally { if (parent != IntPtr.Zero) Marshal.FreeCoTaskMem(parent); if (item != IntPtr.Zero) Marshal.FreeCoTaskMem(item); }
    }
}

[ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItem
{
    void BindToHandler(IntPtr context, ref Guid handler, ref Guid iid, out IntPtr result);
    void GetParent(out IShellItem parent);
    void GetDisplayName(uint type, out IntPtr name);
    void GetAttributes(uint mask, out uint attributes);
    void Compare(IShellItem other, uint hint, out int order);
}
[ComImport, Guid("947AAB5F-0A5C-4C13-B4D6-4BF7836FC9F8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IFileOperation
{
    void Advise(IFileOperationProgressSink sink, out uint cookie);
    void Unadvise(uint cookie);
    void SetOperationFlags(uint flags);
    void SetProgressMessage([MarshalAs(UnmanagedType.LPWStr)] string message);
    void SetProgressDialog(IntPtr dialog);
    void SetProperties(IntPtr properties);
    void SetOwnerWindow(uint hwnd);
    void ApplyPropertiesToItem(IShellItem item);
    void ApplyPropertiesToItems([MarshalAs(UnmanagedType.IUnknown)] object items);
    void RenameItem(IShellItem item, [MarshalAs(UnmanagedType.LPWStr)] string name, IFileOperationProgressSink? sink);
    void RenameItems([MarshalAs(UnmanagedType.IUnknown)] object items, [MarshalAs(UnmanagedType.LPWStr)] string name);
    void MoveItem(IShellItem item, IShellItem destination, [MarshalAs(UnmanagedType.LPWStr)] string? name, IFileOperationProgressSink? sink);
    void MoveItems([MarshalAs(UnmanagedType.IUnknown)] object items, IShellItem destination);
    void CopyItem(IShellItem item, IShellItem destination, [MarshalAs(UnmanagedType.LPWStr)] string? name, IFileOperationProgressSink? sink);
    void CopyItems([MarshalAs(UnmanagedType.IUnknown)] object items, IShellItem destination);
    void DeleteItem(IShellItem item, IFileOperationProgressSink? sink);
    void DeleteItems([MarshalAs(UnmanagedType.IUnknown)] object items);
    void NewItem(IShellItem destination, uint attributes, [MarshalAs(UnmanagedType.LPWStr)] string name, [MarshalAs(UnmanagedType.LPWStr)] string? template, IFileOperationProgressSink? sink);
    void PerformOperations();
    void GetAnyOperationsAborted([MarshalAs(UnmanagedType.Bool)] out bool aborted);
}
[ComVisible(true), Guid("04B0F1A7-9490-44BC-96E1-4296A31252E2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IFileOperationProgressSink
{
    [PreserveSig] int StartOperations();
    [PreserveSig] int FinishOperations(int result);
    [PreserveSig] int PreRenameItem(uint flags, IShellItem item, [MarshalAs(UnmanagedType.LPWStr)] string name);
    [PreserveSig] int PostRenameItem(uint flags, IShellItem item, [MarshalAs(UnmanagedType.LPWStr)] string name, int result, IShellItem? created);
    [PreserveSig] int PreMoveItem(uint flags, IShellItem item, IShellItem destination, [MarshalAs(UnmanagedType.LPWStr)] string? name);
    [PreserveSig] int PostMoveItem(uint flags, IShellItem item, IShellItem destination, [MarshalAs(UnmanagedType.LPWStr)] string? name, int result, IShellItem? created);
    [PreserveSig] int PreCopyItem(uint flags, IShellItem item, IShellItem destination, [MarshalAs(UnmanagedType.LPWStr)] string? name);
    [PreserveSig] int PostCopyItem(uint flags, IShellItem item, IShellItem destination, [MarshalAs(UnmanagedType.LPWStr)] string? name, int result, IShellItem? created);
    [PreserveSig] int PreDeleteItem(uint flags, IShellItem item);
    [PreserveSig] int PostDeleteItem(uint flags, IShellItem item, int result, IShellItem? created);
    [PreserveSig] int PreNewItem(uint flags, IShellItem destination, [MarshalAs(UnmanagedType.LPWStr)] string name);
    [PreserveSig] int PostNewItem(uint flags, IShellItem destination, [MarshalAs(UnmanagedType.LPWStr)] string name, [MarshalAs(UnmanagedType.LPWStr)] string? template, uint attributes, int result, IShellItem? created);
    [PreserveSig] int UpdateProgress(uint total, uint completed);
    [PreserveSig] int ResetTimer();
    [PreserveSig] int PauseTimer();
    [PreserveSig] int ResumeTimer();
}

internal sealed class ShellRecycleBackend : IRecycleBackend
{
    public RecycleResult Recycle(string path, CancellationToken token)
    {
        // Network shares and non-fixed volumes have inconsistent recycle support.
        var drive = new DriveInfo(Path.GetPathRoot(path)!);
        if (path.StartsWith(@"\\") || drive.DriveType != DriveType.Fixed)
            return new(RecycleStatus.Failed, "Chỉ hỗ trợ Thùng rác trên ổ đĩa cục bộ cố định.");
        IFileOperation? operation = null; IShellItem? item = null;
        var sink = new RecycleSink(token);
        try
        {
            operation = (IFileOperation)Activator.CreateInstance(Type.GetTypeFromCLSID(new("3AD05575-8857-4850-9277-11B85BDB8E09"), true)!)!;
            // RECYCLEONDELETE | ALLOWUNDO | EARLYFAILURE | NOCOPYHOOKS |
            // NOERRORUI (required by EARLYFAILURE) | NOCONFIRMATION | SILENT.
            operation.SetOperationFlags(0x00080000 | 0x40 | 0x00100000 | 0x00800000 | 0x400 | 0x10 | 0x4);
            var iid = typeof(IShellItem).GUID;
            ShellInterop.SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out item);
            item.GetAttributes(0x40000000, out var attributes); // SFGAO_FILESYSTEM
            if ((attributes & 0x40000000) == 0) return new(RecycleStatus.Failed, "Mục không thuộc hệ thống file.");
            operation.DeleteItem(item, sink);
            operation.PerformOperations();
            operation.GetAnyOperationsAborted(out var aborted);
            if (aborted || sink.Blocked || token.IsCancellationRequested)
                return new(sink.Succeeded ? RecycleStatus.Partial : RecycleStatus.Canceled, "Shell đã dừng thao tác; hãy quét lại.");
            return sink.Succeeded && sink.Error >= 0 ? new(RecycleStatus.Success) : new(RecycleStatus.Failed, $"Shell không xác nhận recycle (0x{sink.Error:X8}).");
        }
        catch (Exception e) when (e is COMException or IOException or UnauthorizedAccessException)
        { return new(sink.Succeeded ? RecycleStatus.Partial : RecycleStatus.Failed, e.Message); }
        finally { if (item is not null) Marshal.FinalReleaseComObject(item); if (operation is not null) Marshal.FinalReleaseComObject(operation); GC.KeepAlive(sink); }
    }
}
[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
internal sealed class RecycleSink(CancellationToken token) : IFileOperationProgressSink
{
    private const int Abort = unchecked((int)0x80004004);
    public bool Succeeded { get; private set; }
    public bool Blocked { get; private set; }
    public int Error { get; private set; } = unchecked((int)0x80004005);
    public int PreDeleteItem(uint flags, IShellItem item)
    {
        if (token.IsCancellationRequested || (flags & 0x80) == 0) { Blocked = true; return Abort; }
        return 0;
    }
    public int PostDeleteItem(uint flags, IShellItem item, int result, IShellItem? created)
    { Error = result; Succeeded |= result >= 0 && created is not null; return 0; }
    public int StartOperations() => token.IsCancellationRequested ? Abort : 0;
    public int FinishOperations(int result) { if (result < 0) Error = result; return 0; }
    public int PreRenameItem(uint flags, IShellItem item, string name) => Abort;
    public int PostRenameItem(uint flags, IShellItem item, string name, int result, IShellItem? created) => 0;
    public int PreMoveItem(uint flags, IShellItem item, IShellItem destination, string? name) => Abort;
    public int PostMoveItem(uint flags, IShellItem item, IShellItem destination, string? name, int result, IShellItem? created) => 0;
    public int PreCopyItem(uint flags, IShellItem item, IShellItem destination, string? name) => Abort;
    public int PostCopyItem(uint flags, IShellItem item, IShellItem destination, string? name, int result, IShellItem? created) => 0;
    public int PreNewItem(uint flags, IShellItem destination, string name) => Abort;
    public int PostNewItem(uint flags, IShellItem destination, string name, string? template, uint attributes, int result, IShellItem? created) => 0;
    public int UpdateProgress(uint total, uint completed) => token.IsCancellationRequested ? Abort : 0;
    public int ResetTimer() => 0;
    public int PauseTimer() => 0;
    public int ResumeTimer() => 0;
}
