using System.Runtime.InteropServices;
using FolderSize.App.Services;
using FolderSize.Core.Actions;

if (!args.Contains("--recycle-owned-fixtures")) { Console.WriteLine("Opt in with --recycle-owned-fixtures. Creates only owned fixtures."); return 2; }
var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FolderSizeSmoke", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var file = Path.Combine(root, "Tiếng Việt ' file.bin");
var folder = Path.Combine(root, "Thư mục thử");
File.WriteAllBytes(file, [1, 2, 3]); Directory.CreateDirectory(folder); File.WriteAllText(Path.Combine(folder, "owned.txt"), "owned fixture");
Console.WriteLine($"Owned fixture: {root}");
var shell = new WindowsShellService();
foreach (var path in new[] { file, folder })
{
    var result = await shell.RecycleAsync(root, path, default);
    Console.WriteLine($"{Path.GetFileName(path)}: {result.Status} {result.Message}");
    if (result.Status != RecycleStatus.Success || File.Exists(path) || Directory.Exists(path)) return 1;
}
// Verify both original paths are represented in the user's Recycle Bin.
var verified = await VerifyBin(root);
Console.WriteLine(verified ? "PASS: both owned items visible in Recycle Bin; bin was not emptied." : "UNVERIFIED: Recycle Bin entries not found.");
return verified ? 0 : 1;

static Task<bool> VerifyBin(string root)
{
    var source = new TaskCompletionSource<bool>();
    var thread = new Thread(() =>
    {
        object? application = null, bin = null, items = null;
        try
        {
            application = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!);
            dynamic app = application!; bin = app.NameSpace(10); dynamic recycleBin = bin!; items = recycleBin.Items(); dynamic entries = items;
            var found = 0;
            for (var i = 0; i < entries.Count; i++)
            {
                object entry = entries.Item(i);
                try
                {
                    dynamic item = entry;
                    var originalFolder = Convert.ToString(item.ExtendedProperty("System.Recycle.DeletedFrom"));
                    if (string.Equals(originalFolder, root, StringComparison.OrdinalIgnoreCase)) { found++; Console.WriteLine($"Bin entry: {item.Name}"); }
                }
                finally { Marshal.FinalReleaseComObject(entry); }
            }
            source.SetResult(found == 2);
        }
        catch (Exception e) { Console.WriteLine(e.Message); source.SetResult(false); }
        finally
        { if (items is not null) Marshal.FinalReleaseComObject(items); if (bin is not null) Marshal.FinalReleaseComObject(bin); if (application is not null) Marshal.FinalReleaseComObject(application); }
    });
    thread.SetApartmentState(ApartmentState.STA); thread.Start(); return source.Task;
}
