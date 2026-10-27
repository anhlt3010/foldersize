# Kiểm chứng FolderSize

Môi trường: Windows x64, 2026-10-27, SDK cục bộ .NET 10.0.403. SDK tải từ builds.dotnet.microsoft.com, SHA-512 khớp metadata Microsoft.

- Scanner/projection/ViewModel/path/Shell boundary: 39 test PASS (Debug).
- Shell smoke: `dotnet run --project tests/FolderSize.ShellSmoke -- --recycle-owned-fixtures` PASS. Fixture riêng: `C:\Users\LTA\AppData\Local\FolderSizeSmoke\03513e89823542f6b8f991c405aabc39`. File tiếng Việt có dấu nháy và thư mục có file con đã biến mất ở vị trí cũ; cả hai được tìm thấy qua namespace Thùng rác của Windows Shell. Không dọn Thùng rác.
- Microsoft API references: https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ifileoperation-setoperationflags ; https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ifileoperationprogresssink-predeleteitem ; https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/ne-shobjidl_core-_transfer_source_flags
- Giới hạn: kiểm tra đường dẫn trước thao tác không loại hết race khi tiến trình khác thay filesystem đồng thời. Chỉ cho recycle ổ fixed local; từ chối network/removable. Callback PreDelete từ chối nếu thiếu cờ recycle. Không có API fallback permanent.
- UI, DPI, virtualization và bản publish: chưa kiểm chứng.

## Giao diện / ảo hóa
- `dotnet run --project tests/FolderSize.UiSmoke`: PASS. Cây giả 100.000 file = 100.002 dòng khi mở, chỉ 14 ListViewItem thực tế; đóng nhánh = 2 dòng. Giữ được selected row sau mở/đóng.
- Harness đã tái hiện lỗi ProgressBar mặc định TwoWay với ShareValue read-only, sau sửa Mode=OneWay chạy PASS. Test batch đã FAIL với 1000 collection events, sau sửa còn 1 event và suite PASS.
- Đã render và xem ảnh `artifacts/ui/empty.png`, `expanded.png`, `minimum.png`, `layout-150.png`. DPI desktop thực = 150%; kích thước mặc định 1200x760 và tối thiểu 900x600. LayoutTransform 150% là kiểm tra bố cục mô phỏng, không phải thay DPI hệ thống. Desktop DPI 100% chưa được kiểm chứng riêng.
- Computer Use đã mở/quan sát trạng thái trống. Khi nhập fixture, công cụ hai lần báo người dùng đang thao tác nên dừng nhập UI. Tương tác bàn phím và chọn thư mục trực tiếp chưa xác minh; logic lệnh được kiểm tra bằng tests/harness. Không thay cài đặt DPI người dùng.

## Release / tích hợp cuối
- 44/44 tests Release PASS; build Release không cảnh báo/lỗi.
- Shell smoke Release bổ sung physical ViewModel → xác nhận fixture → Shell recycle → tự quét lại: PASS, còn root 0 byte. Explorer API PASS sau khi sửa lỗi entry point ILCloneFull (smoke đã FAIL trước sửa). Dùng SHParseDisplayName riêng cho PIDL cha.
- Fixture lần cuối: `C:\Users\LTA\AppData\Local\FolderSizeSmoke\b2daa8a9a8c3438a87e1f3e2988ab548`; cả 3 mục được thấy trong Thùng rác bằng thuộc tính DeletedFrom trùng fixture. Không dọn Thùng rác.
- Các ca success/partial rescan, lỗi/hủy, hủy xác nhận, stale, root không recycle được và không tuyên bố giải phóng chỗ đã được kiểm tra bằng unit/integration tests.
- Ảnh đã xem: [cây mở](images/foldersize-expanded.png), [cửa sổ tối thiểu](images/foldersize-minimum.png).
- Chưa kiểm chứng riêng: DPI desktop 100%, bộ bước nhập/chọn folder/bàn phím/recycle trực tiếp trên exe publish và volume không hỗ trợ recycle. Không ghi những mục này là PASS.

## Rà soát cuối
- Review độc lập toàn nhánh: không có Critical; 1 Important — CanRecycle tạo separator kép khi gốc quét là ổ đĩa. Test `DriveRootCannotBeRecycledButItsDescendantCan` đã FAIL đúng điều kiện, sau sửa PASS. Toàn bộ **45/45 test Release PASS**, build Release **0 warnings / 0 errors**, `git diff --check` PASS. Không có minor hoãn lại, không có mục reviewer từ chối đánh giá.
- Exe portable đã khởi chạy và được quan sát trạng thái trống bằng accessibility. ZIP gồm 402 entries, có exe, coreclr.dll, PresentationFramework.dll và app DLL. Chưa thực hiện toàn bộ chuỗi thao tác UI trên exe publish như đã nêu ở trên.

## Nhận diện / credit LTA — 2026-10-27
- App v1.1.0: icon riêng từ vector trong repo, ICO 9 kích thước 16/20/24/32/40/48/64/128/256 px. Header/Giới thiệu dùng PNG256 để tránh WPF chọn frame ICO nhỏ rồi phóng lớn.
- Company/Authors = LTA, copyright © 2026 LTA; đã đọc metadata Win32 của exe và kiểm tra cửa sổ Giới thiệu.
- WindowsIconTests: đã thấy 2 test FAIL trước implementation; sau implementation, toàn suite 47/47 Release PASS. Test dùng Windows thật, kiểm tra icon đóng/mở/ổ đĩa, frozen BitmapSource, cache tái sử dụng và loại file khi đường dẫn không tồn tại.
- UI smoke: ICO decode đủ9frames; Window.Icon có resource; mọi ListViewItem hiện thực đều có Image.Source Windows. 100.000 file vẫn chỉ14containers, giữ lựa chọn và thu gọn đúng2dòng. Đã xem ảnh mở cây và Giới thiệu ở DPI150%.
- Icon Windows: SHGetStockIconInfo/SHGetFileInfoW với SHGFI_USEFILEATTRIBUTES; chỉ đưa tên giả + phần mở rộng, không đưa đường dẫn file thực; giải phóng HICON bằng DestroyIcon, bitmap frozen, cache tối đa128mục. Icon gắn tại template dòng nhìn thấy, không gắn cho toàn bộ100k node.
- Nguồn API Microsoft: https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-shgetstockiconinfo ; https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-shgetfileinfow
- Trong lần đóng gói đầu của thay đổi icon, test trạng thái cũ lỗi không ổn định (ShellCancelOrFailureIsNotSuccess). Đã điều tra và tái hiện bằng `HeadlessProgressCannotClearSelectionAfterCompletion`: callback Progress<T> chạy thread pool khi không có SynchronizationContext có thể vượt generation guard rồi xóa lựa chọn sau completion. Test mới FAIL (selected row thành null), sau sửa chỉ phát progress khi có context đồng bộ, suite48/48 Release và UI100k PASS. WPF vẫn có context giao diện và nhận progress; runner headless nhận kết quả cuối.
- Review độc lập phần icon/native/resource/credit: không có Critical/Important, không có mục từ chối đánh giá. Cache128, frozen BitmapSource, HICON lifetime và template chỉ dòng hiện thực đã được rà soát.
