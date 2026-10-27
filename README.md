# FolderSize

Ứng dụng Windows 10/11 x64 để tìm file và thư mục chiếm nhiều dung lượng, mở vị trí trong Explorer và chuyển từng mục vào Thùng rác.

## Chạy bản portable

Giải nén **toàn bộ** `FolderSize-win-x64.zip` vào một thư mục rồi mở `FolderSize.App.exe`. Giữ các DLL và thư mục đi kèm exe. Không cần cài .NET riêng, không cần quyền quản trị.

1. Bấm **Chọn thư mục…** hoặc nhập đường dẫn tuyệt đối của thư mục/ổ đĩa và bấm **Quét** (Enter trong ô đường dẫn).
2. Mở/thu nhánh bằng nút mũi tên; khi chọn dòng, phím Trái/Phải cũng đóng/mở được. Sau quét, các mục trong mỗi cấp xếp giảm dần theo dung lượng.
3. Chọn một mục để xem đường dẫn và dung lượng, mở Explorer hoặc chuyển vào Thùng rác. Luôn có hộp xác nhận, mặc định từ chối. Không xóa gốc vùng quét; muốn quản lý một thư mục, hãy quét thư mục cha.
4. **Hủy** giữ kết quả tạm, khóa thao tác xóa. Sau recycle thành công/một phần, ứng dụng tự quét lại; lỗi hoặc kết quả cũ chưa chắc chắn được đánh dấu cần quét lại.

## Hiểu kết quả

- **Kích thước logic** là tổng số byte của file theo metadata, không phải số byte thực chiếm trên ổ. Dùng hệ 1024: KiB, MiB, GiB, TiB. Vì nén, sparse file, hard link và metadata hệ thống, kết quả có thể khác phần Used của Windows.
- Hard link được tính theo mỗi đường dẫn, nên có thể tính lặp dung lượng.
- Không đi theo junction, symbolic link hoặc reparse point. File reparse/đám mây cũng bị bỏ qua, không tải nội dung file để tính kích thước.
- Lỗi truy cập, file biến mất và nhánh bị bỏ qua được ghi trong **Xem chi tiết**. Nhãn **Chưa đầy đủ** lan lên thư mục cha. Số file/thư mục và tỷ lệ chỉ dựa trên dữ liệu đã đọc được; thư mục cha bằng 0 hiển thị “—”.
- Kết quả là ảnh chụp tại thời điểm quét. Nội dung có thể đã thay đổi trước khi xác nhận recycle. Kiểm tra đường dẫn không loại bỏ hoàn toàn race khi ứng dụng khác đồng thời thay đổi filesystem.
- Chỉ hỗ trợ recycle trên ổ cục bộ cố định. Không recycle trên network/removable. Không có chế độ xóa vĩnh viễn và không fallback sang xóa vĩnh viễn.
- **Chuyển vào Thùng rác chưa giải phóng dung lượng.** Bạn tự quản lý hoặc khôi phục dữ liệu trong Thùng rác Windows.

Ứng dụng chỉ xử lý dữ liệu cục bộ, không gửi nội dung hoặc đường dẫn ra mạng, không lưu lịch sử, không tự dọn dữ liệu.

## Build dành cho developer

Cần SDK .NET **10.0.403** theo `global.json`. Có thể đặt SDK vào `.tools/dotnet` để tránh cài toàn máy. Build script dùng SDK cục bộ trước, đặt cache/tạm trong `.work`.

```powershell
./scripts/Build.ps1
./scripts/Build.ps1 -Publish
```

Kết quả: `artifacts/FolderSize-win-x64/FolderSize.App.exe` và `artifacts/FolderSize-win-x64.zip`. Bản phát hành self-contained, không trimming WPF.

Kiểm thử Shell chỉ dùng fixture do runner tự tạo; không xóa dữ liệu cá nhân hoặc dọn Thùng rác:

```powershell
./.tools/dotnet/dotnet.exe run --project tests/FolderSize.ShellSmoke -- --recycle-owned-fixtures
./.tools/dotnet/dotnet.exe run --project tests/FolderSize.UiSmoke
```

Xem [bằng chứng và giới hạn kiểm tra](docs/verification.md).
