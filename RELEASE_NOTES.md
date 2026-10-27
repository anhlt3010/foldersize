# Ghi chú phát hành FolderSize

Tài liệu ghi lại thay đổi trong các phiên bản mã nguồn. Các mục bên dưới không đồng nghĩa với việc đã tạo tag hoặc đăng GitHub Release.

## 1.1.1 — 27/10/2026

**Cải thiện nút mở/thu thư mục để cây gọn và dễ nhìn hơn.**

### Thay đổi

- Thay nút có khung viền bằng mũi tên vector nét mảnh, hướng phải khi đóng và hướng xuống khi mở.
- Thêm nền xanh nhạt và đổi màu mũi tên khi rê chuột hoặc nhấn.
- File và thư mục rỗng không còn hiển thị ô nút trống; giữ khoảng trống để căn các mục trong cây.
- Thêm tooltip và tên accessibility theo trạng thái: “Mở thư mục” hoặc “Thu gọn thư mục”.
- Giữ thao tác đóng/mở bằng phím Trái/Phải và các icon Windows đã có.
- Bổ sung MIT License với copyright thuộc LTA; tệp `LICENSE` được đưa vào gói publish.

### Kiểm tra

- **48/48 test Release đạt.**
- UI smoke với 100.000 file: 100.002 dòng dữ liệu khi mở, chỉ 14 container giao diện được tạo trong viewport kiểm tra.
- Chỉ hai dòng thư mục có nội dung hiển thị nút mở/thu trong fixture; các dòng file không còn nút trống.
- Thu nhánh qua command của nút đưa danh sách về hai dòng và giữ lựa chọn.
- Đã xem ảnh ở DPI desktop 150%, cửa sổ mặc định và kích thước tối thiểu.

### Gói portable và nâng cấp

- Gói phiên bản: `FolderSize-1.1.1-win-x64.zip`.
- Giải nén toàn bộ gói vào thư mục riêng và mở `FolderSize.App.exe`; không cần cài runtime .NET riêng.
- Bản hiện tại không lưu lịch sử quét hoặc cơ sở dữ liệu nên không cần chuyển dữ liệu giữa các phiên bản. Quét lại sau khi mở bản mới.
- Đóng bản cũ trước khi thay tệp trong cùng thư mục, hoặc dùng thư mục riêng để tránh DLL bị Windows khóa.

### Giới hạn còn lại

- Chưa kiểm chứng riêng DPI desktop 100% và toàn bộ chuỗi thao tác UI trực tiếp trên exe publish. Các kết quả unit test, UI harness và Shell smoke có phạm vi được ghi riêng trong [tài liệu kiểm chứng](docs/verification.md).
- Recycle chỉ hỗ trợ ổ cục bộ cố định; từ chối ổ mạng và ổ rời. Không có fallback xóa vĩnh viễn.
- Dung lượng là kích thước logic tại thời điểm quét, có thể khác dung lượng thực trên ổ. Kết quả có thể chưa đầy đủ nếu có lỗi hoặc mục bị bỏ qua.

## 1.1.0 — 27/10/2026

### Bổ sung

- Icon riêng cho FolderSize, nhúng vào exe và cửa sổ; ICO có chín kích thước từ 16 đến 256 px.
- Icon Windows Shell cho thư mục đóng/mở, ổ đĩa và loại file. Dùng cache tối đa 128 mục, chỉ yêu cầu icon cho các dòng giao diện hiện thực.
- Cửa sổ **Giới thiệu**, hiển thị phiên bản, credit và **© 2026 LTA**.
- Metadata của exe ghi Product/Company/Authors và copyright LTA.

### Sửa lỗi

- Tránh callback tiến độ chạy đồng thời rồi xóa lựa chọn sau khi quét xong trong môi trường không có `SynchronizationContext`. Giao diện WPF vẫn nhận tiến độ qua context giao diện; runner headless nhận kết quả cuối.

### Kiểm tra

- Bổ sung test icon Windows và test tái hiện lỗi lựa chọn; tổng bộ test đạt 48 ca.
- Xác minh icon nhiều kích thước, credit trong metadata và khả năng ảo hóa với cây 100.000 file.

## 1.0.0 — Nền tảng ban đầu, 27/10/2026

- Ứng dụng WPF portable cho Windows x64, giao diện tiếng Việt.
- Quét thư mục/ổ đĩa trên worker, có hủy và giữ kết quả tạm; ghi nhận lỗi từng nhánh và lan trạng thái chưa đầy đủ lên cha.
- Cây file/thư mục có ảo hóa, sắp xếp dung lượng giảm dần theo cấp và tỷ lệ thư mục cha.
- Mở vị trí bằng Windows Shell; recycle từng mục sau xác nhận và tự quét lại sau kết quả thành công hoặc một phần.
- Kiểm tra phạm vi đường dẫn và reparse point trước recycle; chặn gốc quét và gốc ổ đĩa.
- Sửa điều kiện đường dẫn để mục con vẫn có thể được recycle khi quét toàn bộ ổ đĩa.
- Build self-contained để người dùng không cần cài .NET riêng.

---

**FolderSize được phát triển bởi LTA. © 2026 LTA.** Phát hành theo [MIT License](LICENSE).
