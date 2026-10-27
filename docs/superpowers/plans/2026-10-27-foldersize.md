# FolderSize Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Tạo app Windows portable giúp tìm và chuyển các mục chiếm nhiều dung lượng vào Thùng rác qua cây thư mục trực quan.

**Architecture:** WPF/MVVM nhỏ gọn, dịch vụ quét và Windows Shell độc lập với giao diện. Quét sinh mô hình cây dữ liệu; giao diện dùng danh sách dòng phẳng ảo hóa chỉ chứa các nhánh đang mở, tránh tạo control cho toàn bộ file. Trạng thái điều phối chặn thao tác xung đột và chỉ cập nhật sau khi có kết quả thật.

**Tech Stack:** C#, .NET 10 LTS, WPF, Windows Shell IFileOperation qua COM interop; xUnit cho kiểm thử logic. Không dùng thư viện giao diện bên ngoài.

**Spec:** `docs/superpowers/specs/2026-10-27-foldersize-design.md` (đã được duyệt ngày 2026-10-27).

## Global Constraints

- Mục tiêu Windows 10/11 x64.
- Giao diện tiếng Việt, nền sáng, hỗ trợ co giãn cửa sổ và thao tác bàn phím cơ bản.
- Dung lượng là tổng kích thước logic của file theo byte, không phải dung lượng thực chiếm trên ổ.
- Không đi theo directory reparse point, junction hoặc symbolic link để tránh vòng lặp và vượt phạm vi.
- Không tải nội dung file đám mây để tính kích thước.
- Không tự yêu cầu quyền quản trị.
- Xóa vĩnh viễn không nằm trong bản đầu.
- Cấm xóa gốc vùng quét và gốc ổ đĩa.
- Phát hành bản portable self-contained cho Windows x64, chạy được không cần cài .NET riêng.
- Ứng dụng chỉ xử lý dữ liệu cục bộ, không gửi đường dẫn hay nội dung file lên mạng.
- Không installer, lịch sử quét, tự cập nhật, tự dọn, chọn nhiều mục hoặc biểu đồ trong bản đầu.

## Review Focus

1. Đường dẫn có Unicode, khoảng trắng, dấu nháy và tiền tố giống nhau: mở đúng vị trí, không cho xóa mục ngoài gốc (Task 3).
2. Một nhánh từ chối truy cập hoặc biến mất trong lúc enumerate: các nhánh còn lại vẫn được tính, cha mang trạng thái thiếu (Task 1).
3. Hủy khi đang duyệt nhánh sâu: kết thúc nhanh, giữ kết quả tạm, không cho xóa (Task 2).
4. Thư mục rỗng, kích thước 0 và file bằng dung lượng: không chia 0, thứ tự ổn định và bộ nhớ giao diện không tăng theo nhánh đóng (Task 2/4).
5. Mục bị đổi thành junction hoặc cha bị thay thế sau lần quét: kiểm tra lại đường đi trước Shell; thất bại/hủy hoặc recycle không khả dụng tuyệt đối không fallback permanent (Task 3/5).

## File map

- `FolderSize.sln`, `global.json`, `.gitignore`: solution, SDK .NET 10, bỏ qua bin/obj/.tools/.work/.superpowers/artifacts.
- `scripts/Build.ps1`: cấu hình TEMP/TMP, DOTNET_CLI_HOME, NUGET_PACKAGES trong workspace; restore/test/publish bằng SDK cục bộ nếu có.
- `src/FolderSize.Core/FolderSize.Core.csproj`: net10.0, logic không phụ thuộc WPF.
- `src/FolderSize.Core/Scanning/ScanNode.cs`, `ScanResult.cs`, `ScanProgress.cs`: dữ liệu quét, issue, trạng thái incomplete/canceled.
- `src/FolderSize.Core/Scanning/IFileSystem.cs`, `PhysicalFileSystem.cs`, `DirectoryScanner.cs`: seam đọc metadata/enumeration và thuật toán quét.
- `src/FolderSize.Core/Presentation/SizeFormatter.cs`, `TreeProjection.cs`: format và chiếu cây sang dòng nhìn thấy.
- `src/FolderSize.Core/Actions/PathPolicy.cs`, `IShellService.cs`: điều kiện xóa và hợp đồng thao tác.
- `src/FolderSize.App/FolderSize.App.csproj`, `App.xaml`, `App.xaml.cs`: app net10.0-windows, WPF.
- `src/FolderSize.App/Infrastructure/ObservableObject.cs`, `RelayCommand.cs`: binding và command.
- `src/FolderSize.App/ViewModels/MainViewModel.cs`, `NodeRowViewModel.cs`: điều phối scan/selection/projection.
- `src/FolderSize.App/Services/WindowsShellService.cs`, `ShellInterop.cs`, `FolderPicker.cs`, `UserDialogs.cs`: nền tảng Windows, COM recycle-only trên STA riêng, hộp thoại UI.
- `src/FolderSize.App/MainWindow.xaml`, `MainWindow.xaml.cs`, `Themes/Styles.xaml`: cửa sổ và style; code-behind chỉ xử lý tương tác view.
- `tests/FolderSize.Tests/FolderSize.Tests.csproj`, `ScanningTests.cs`, `ProjectionTests.cs`, `PathPolicyTests.cs`, `MainViewModelTests.cs`: test logic; thêm test project reference app và target net10.0-windows khi Task 2 bắt đầu.
- `tests/FolderSize.ShellSmoke/FolderSize.ShellSmoke.csproj`, `Program.cs`: smoke test Shell opt-in chỉ trên fixture tự tạo.
- `docs/verification.md`, `README.md`: bằng chứng kiểm tra và hướng dẫn chạy/phát hành.

## Task 1: Quét dung lượng chính xác và chịu lỗi

**Interfaces:**
- `FileEntry(string FullPath, string Name, bool IsDirectory, FileAttributes Attributes, long Length)`.
- `IFileSystem.ReadEntry(string path): FileEntry`, `Enumerate(string directory): IEnumerable<string>`; seam cho phép giả lập lỗi cả lúc bắt đầu và giữa enumeration.
- `ScanNode`: `FullPath`, `Name`, `IsDirectory`, `SizeBytes:long`, `FileCount:long`, `DirectoryCount:long`, `IsIncomplete:bool`, `IsSkipped:bool`, `Children:List<ScanNode>`; số thư mục không gồm chính node; file có FileCount=1.
- `ScanIssue(string Path, string Reason)`; `ScanResult(ScanNode Root, bool IsCanceled, IReadOnlyList<ScanIssue> Issues)`.
- `ScanProgress(string CurrentPath, long SizeBytes, long FileCount, long DirectoryCount, ScanNode Snapshot)`; snapshot tách khỏi cây đang mutate, phát tối đa mỗi 500ms và khi kết thúc, không clone toàn bộ cây mỗi file.
- `DirectoryScanner(IFileSystem fs).ScanAsync(string root, IProgress<ScanProgress>? progress, CancellationToken token): Task<ScanResult>`.

- [ ] Chuẩn bị SDK .NET 10 x64 bản servicing mới nhất từ nguồn Microsoft, cài vào `.tools/dotnet` nếu chưa có. Không cài toàn máy. Đặt các thư mục tạm/cache trong `.work`; dùng đường dẫn exe tuyệt đối, `--version` phải là 10.0.x. Nếu tải mạng bị chặn thì báo đúng blocker, không tự đổi sang .NET đã hết hỗ trợ khi phát hành.
- [ ] Khởi tạo Git nếu chưa có; tạo solution/core/tests và script build, pin SDK đã xác minh trong global.json. Không tạo product view trước Task 4. Commit spec đã duyệt và plan; ignore dữ liệu mockup/cache. Mọi commit sau từng task chỉ stage file của task, không stage fixture người dùng.
- [ ] Viết test đầu tiên `CountsNestedFiles`: fixture giả có root/a.bin=1024, root/sub/b.bin=2048, root/empty; assert root.SizeBytes=3072, FileCount=2, DirectoryCount=2, sub.SizeBytes=2048. Chạy `dotnet test --filter CountsNestedFiles`, xác nhận FAIL do chưa có implementation.
- [ ] Implement model, PhysicalFileSystem đọc metadata không mở nội dung, scanner dùng stack thay vì recursive call stack, không follow reparse point, chạy trên worker thread và tích lũy tổng bottom-up.
- [ ] Thêm từng test trước khi thêm nhánh xử lý: `DeniedBranchMarksAncestorsIncomplete` (nhánh lỗi không cản sibling=2048); `EnumerationFailsMidwayKeepsKnownChildren`; `VanishedFileIsIssue`; `ReparseNodesAreSkippedWithoutEnumeration`; `ZeroByteRootHasZeroSize`; `HardLinkPathsAreCountedSeparately`; `CancelReturnsPartialAndCanceled`. Fake metadata không được gọi API mở file. Invalid/missing root phải trả lỗi rõ cho caller, không coi là scan thành công rỗng.
- [ ] Chạy `dotnet test --filter ScanningTests`; tất cả PASS. Chạy thêm test PhysicalFileSystem trên thư mục temp tự tạo chứa tên tiếng Việt và file nhị phân kích thước biết trước, không quét dữ liệu cá nhân.
- [ ] Commit `feat: add cancellable directory size scanner`.

## Task 2: Điều phối và phép chiếu cây

**Interfaces:**
- `TreeProjection.Flatten(ScanNode root, ISet<string> expandedPaths, bool sortBySize): IReadOnlyList<NodeRow>`; NodeRow có `Node`, `Depth`, `HasChildren`, `IsExpanded`, `ParentShare:double?` (0..100), ID bằng path Windows case-insensitive. Tie-break tên OrdinalIgnoreCase rồi Ordinal.
- `SizeFormatter.Format(long bytes): string` dùng 1024 và culture vi-VN, đơn vị B/KiB/MiB/GiB/TiB.
- `MainViewModel(DirectoryScanner scanner, IShellService shell, IUserDialogs dialogs)`; public `PathInput`, `Rows`, `SelectedRow`, `State`, `StatusText`, `Issues`, `CanScan`, `CanCancel`, `CanRecycle`; `ScanAsync()`, `Cancel()`, `ToggleExpanded(NodeRowViewModel row)`, `RecycleSelectedAsync()`, `OpenSelectedAsync()`.
- `AppState`: Empty, Scanning, Completed, Canceled, Recycling, Failed, Stale. UI cập nhật qua dispatcher/synchronization context, mọi async exception được xử lý.
- `IUserDialogs.ConfirmRecycleAsync(string path, long scannedBytes): Task<bool>`, `ShowError(string title,string message)`; method recycle dùng Task 3.

- [ ] Tạo project app tối thiểu để test ViewModel, không viết giao diện chính. Viết `FlattenOnlyExpandedBranches` với root/sub/file: ban đầu 2 dòng, mở sub=3, đóng lại=2; chạy test xác nhận FAIL.
- [ ] Implement TreeProjection và SizeFormatter; thêm test `SortSiblingsDescendingWithStableTies`, `ShareOfEmptyParentIsNull`, `Format1024IsOneKiB`, `FileCountsDoNotIncludeDirectories`. Giữ thứ tự ghi nhận khi scan chưa xong, chỉ sort hoàn tất.
- [ ] Viết test ViewModel với scanner fake qua IFileSystem và shell/dialog fake: `CancelKeepsRowsAndDisablesRecycle`, `BusyPreventsSecondScan`, `InvalidPathShowsError`, `ProgressDoesNotReplaceSelectionUnnecessarily`, `CompletionEnablesOnlyEligibleSelection`. Chạy để thấy FAIL rồi implement observable properties/commands và state transitions.
- [ ] Snapshot tiến độ không được dùng chung mutable collection với worker; áp dụng diff/batch trên UI để giữ selected/expanded paths. Chỉ duy trì các dòng đang hiển thị. Thêm test `LateProgressCannotOverwriteNewerCompletedScan` bằng scan generation ID.
- [ ] Chạy `dotnet test --filter "ProjectionTests|MainViewModelTests"` và `dotnet build`; tất cả PASS.
- [ ] Commit `feat: add scan state and virtual tree projection`.

## Task 3: Explorer và Thùng rác an toàn

**Interfaces:**
- `PathPolicy.ValidateRecycle(string root, string candidate, IFileSystem fs): PathValidation` (`Allowed`, `Reason`). Normalize absolute Windows paths, so sánh ranh giới separator và OrdinalIgnoreCase, kiểm tra từng component tới mục đích không reparse; không chấp nhận root, volume root, path ngoài vùng, mục không tồn tại.
- `IShellService.OpenInExplorerAsync(string path): Task`; `RecycleAsync(string root, string path, CancellationToken token): Task<RecycleResult>`.
- `RecycleResult(RecycleStatus Status, string? Message)`, status Success/Canceled/Failed/Partial; cancellation không khẳng định rollback nội dung đã được Shell xử lý.
- `WindowsShellService` kiểm tra PathPolicy ngay trước thao tác; COM `IFileOperation`, `IShellItem`, progress sink để ghi nhận kết quả mục và `GetAnyOperationsAborted`.

- [ ] Viết `RejectsRootOutsidePrefixAndReparseAncestors`, assert chặn D:/data, D:/database/a khi root D:/data, D:/other, ổ gốc và reparse ở cha; chấp nhận D:/data/sub/file với biến thể hoa/thường và dấu separator chuẩn. Thêm Unicode, khoảng trắng, dấu nháy, trailing slash, dot segments. Chạy FAIL rồi implement PathPolicy.
- [ ] Viết fake-Shell tests bảo đảm dialog từ chối không gọi Shell, revalidation fail không gọi DeleteItem, Shell cancel không được coi success, lỗi recycle không gọi bất kỳ permanent-delete API nào. Implement action orchestration; không dùng File.Delete/Directory.Delete cho dữ liệu người dùng.
- [ ] Implement COM STA worker và interop dựa trên tài liệu Windows SDK chính thức. Bật `FOFX_RECYCLEONDELETE`, `FOF_ALLOWUNDO`, `FOFX_EARLYFAILURE`, `FOFX_NOCOPYHOOKS` và chặn UI fallback permanent (kiểm chứng bằng smoke test). Chỉ thao tác filesystem item. Giữ own confirmation; không suppress error theo cách khiến lỗi bị hiểu là thành công. Nếu API/flags không bảo đảm recycle-only thì dừng thao tác với lỗi thay vì thay bằng Delete khác.
- [ ] Mở Explorer bằng Shell item selection API `SHOpenFolderAndSelectItems` (PIDL), tránh nội suy đường dẫn thành command string; giải phóng COM/PIDL trong finally.
- [ ] Tạo smoke runner opt-in: tự tạo fixture dưới temp local, bảo đảm fixture không phải junction/ngoài vị trí đã xác thực; recycle một file và một thư mục, verify biến mất ở path cũ và có thể thấy/khôi phục từ Thùng rác. Không tự dọn sạch Thùng rác. Trường hợp recycle không hỗ trợ (network/volume) phải trả Failed mà không xóa file; không dùng dữ liệu thật để thử.
- [ ] Chạy `dotnet test --filter PathPolicyTests` PASS; chạy smoke bằng `dotnet run --project tests/FolderSize.ShellSmoke -- --recycle-owned-fixtures` trong phiên desktop và ghi bằng chứng. Không ghi PASS nếu phiên không có Shell hoặc không thể xác minh recycle.
- [ ] Commit `feat: integrate Explorer and recycle-only shell actions`.

## Task 4: Giao diện WPF theo mockup

**Consumes:** MainViewModel và NodeRowViewModel từ Task 2, Shell từ Task 3. **Produces:** app sử dụng thật; FolderPicker dùng OpenFolderDialog của WPF; UserDialogs triển khai hợp đồng Task 2.

- [ ] Tạo App/MainWindow/Styles theo bố cục mockup `.superpowers/brainstorm/292-1793073963/content/layout.html`: path input, chọn folder, scan/cancel, summary, bảng cây và details pane. Không phụ thuộc mockup lúc runtime.
- [ ] Dùng ListView/GridView với VirtualizingStackPanel.IsVirtualizing=true, VirtualizationMode=Recycling, CanContentScroll=true; không bọc thêm ScrollViewer phá virtualization. Dòng có indent theo Depth, nút toggle riêng và tên file/thư mục, size, share bar, file count. Selection một mục.
- [ ] Binding commands và enable states; Enter quét path, Left/Right đóng/mở nhánh, Tab focus rõ; mặc định 1200x760, tối thiểu 900x600. Đường dẫn dài có tooltip, summary và details không bị tràn. Dialog issue hiển thị các mục lỗi bằng danh sách ảo hóa.
- [ ] Hoàn thiện empty/scanning/canceled/completed/incomplete/failed states; progress không có phần trăm giả; label “Kích thước logic” và chú thích tỷ lệ chỉ tính dữ liệu đọc được. Panel xóa cho biết thư mục bao gồm nội dung con; không cho xóa gốc và nêu lý do.
- [ ] Chạy `dotnet build`; mở app với fixture Task 1, kiểm tra so với mockup ở 100% và 150% DPI, resize, bàn phím, đường dẫn dài. Tạo fixture giả 100.000 nodes qua test-only harness (không ship switch demo), đóng nhánh lớn rồi mở; verify số container thực tế gần số dòng viewport, không bằng tổng file. Ghi kết quả trong docs/verification.md.
- [ ] Commit `feat: build Vietnamese disk usage interface`.

## Task 5: Tích hợp xóa, cập nhật lại và nghiệm thu

**Files:** MainViewModel.cs, MainViewModelTests.cs, README.md, docs/verification.md, scripts/Build.ps1; sửa các file liên quan khi có lỗi cụ thể.

- [ ] Viết `ConfirmedRecycleRescansRoot`, `CanceledConfirmationDoesNotCallShell`, `PartialRecycleRescansAndShowsWarning`, `ShellFailureMarksStaleWhenUncertain`, `RecycleDisabledDuringScanOrCanceledScan`, `RecycleDoesNotClaimDiskSpaceFreed`; dùng fake shell và fixture. Chạy FAIL rồi hoàn thiện confirmation/recycle/rescan state transitions. Khi re-scan lỗi/hủy, kết quả cũ phải bị đánh dấu stale và chặn xóa.
- [ ] Xác nhận dialog nêu path đầy đủ, size tại lần scan, “nội dung có thể đã thay đổi” và nội dung con; root không có nút xóa khả dụng. Khi Shell đang chạy khóa scan/delete, giữ UI phản hồi. Thay thế path thành reparse trước gọi Shell phải bị chặn; tài liệu ghi giới hạn thay đổi filesystem đồng thời không thể loại hết race bằng kiểm tra path.
- [ ] Chạy toàn bộ `dotnet test`, build Release và smoke fixture; mọi FAIL phải được sửa trước đóng task. Review code xóa để không có fallback permanent, không shell-command injection, không nuốt exceptions.
- [ ] Publish: `dotnet publish src/FolderSize.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o artifacts/FolderSize-win-x64`. Không trimming WPF. Đóng gói toàn thư mục publish thành zip, không chỉ lấy exe.
- [ ] Mở exe đã publish và thử empty → chọn folder → scan → mở/đóng → Explorer → hủy xác nhận → recycle fixture → re-scan; kiểm tra lỗi quyền, mục biến mất và hủy scan. Ghi môi trường, lệnh, kết quả thực tế và ảnh UI trong docs/verification.md; điểm nào chưa chạy phải ghi rõ chưa xác minh.
- [ ] README tiếng Việt: cách mở exe, chọn folder/ổ, nghĩa kích thước logic, liên kết bị bỏ qua, hardlink tính lặp, kết quả thiếu, Thùng rác chưa giải phóng chỗ, cách build và yêu cầu SDK chỉ cho developer.
- [ ] `git diff --check`, chạy đúng các kiểm tra bị ảnh hưởng sau sửa cuối; commit `feat: finish portable FolderSize desktop app`. Báo đường dẫn exe/zip và giới hạn kiểm tra; không merge/publish remote ngoài phạm vi.

## Self-review

Mọi phần spec có task: scan/metadata/issues=1; projection/state=2; path/Shell safety=3; UI/keyboard/virtualization=4; confirmation/rescan/release/help=5. Các interface dùng thống nhất ScanNode, ScanResult, RecycleResult và AppState. Năm Review Focus đều có test hoặc smoke cụ thể. Toolchain hiện chỉ có SDK 8.0.407; .NET 10 download và runtime pack restore là phụ thuộc môi trường cần xác minh khi bắt đầu, chưa được cài trong giai đoạn lập kế hoạch. Thư mục chưa là Git repo nên tài liệu chưa commit; Task 1 sẽ khởi tạo và commit.
