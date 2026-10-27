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
