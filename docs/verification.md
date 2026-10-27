# Kiểm chứng FolderSize

Môi trường: Windows x64, 2026-10-27, SDK cục bộ .NET 10.0.403. SDK tải từ builds.dotnet.microsoft.com, SHA-512 khớp metadata Microsoft.

- Scanner/projection/ViewModel/path/Shell boundary: 39 test PASS (Debug).
- Shell smoke: `dotnet run --project tests/FolderSize.ShellSmoke -- --recycle-owned-fixtures` PASS. Fixture riêng: `C:\Users\LTA\AppData\Local\FolderSizeSmoke\03513e89823542f6b8f991c405aabc39`. File tiếng Việt có dấu nháy và thư mục có file con đã biến mất ở vị trí cũ; cả hai được tìm thấy qua namespace Thùng rác của Windows Shell. Không dọn Thùng rác.
- Microsoft API references: https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ifileoperation-setoperationflags ; https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ifileoperationprogresssink-predeleteitem ; https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/ne-shobjidl_core-_transfer_source_flags
- Giới hạn: kiểm tra đường dẫn trước thao tác không loại hết race khi tiến trình khác thay filesystem đồng thời. Chỉ cho recycle ổ fixed local; từ chối network/removable. Callback PreDelete từ chối nếu thiếu cờ recycle. Không có API fallback permanent.
- UI, DPI, virtualization và bản publish: chưa kiểm chứng.
