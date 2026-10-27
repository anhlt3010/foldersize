# FolderSize: Tiếng Việt / English

Người dùng đồng ý bổ sung lựa chọn Tiếng Việt / English và lưu lựa chọn cho lần mở sau. Giao diện đổi ngay, không yêu cầu quét lại. Đây là thay đổi trong dự án WPF hiện có, tiếp tục làm tại checkout hiện tại theo lựa chọn trước đó.

- Bộ chọn ở góc trên bên phải, tên hai lựa chọn luôn là Tiếng Việt / English. Mặc định tiếng Việt khi chưa có lựa chọn hợp lệ.
- Dịch nhãn, cột, tooltip, trạng thái, trợ giúp, giới thiệu, xác nhận recycle và các lỗi do app định nghĩa. Hộp thoại/native error do Windows cung cấp vẫn theo ngôn ngữ hệ điều hành.
- Số và kích thước theo vi-VN / en-US. Không đổi tên file, đường dẫn, dữ liệu, thứ tự sắp xếp, nhánh mở hoặc lựa chọn. Cho đổi khi đang quét.
- Chỉ lưu mã ngôn ngữ trong `%LOCALAPPDATA%/FolderSize/settings.json`. Tệp thiếu/hỏng không chặn mở app. Nếu lưu lỗi, vẫn đổi UI và thông báo không lưu được. Không ghi cạnh exe hay lưu lịch sử quét.
- Một catalog JSON nhúng, một LanguageService có notification và một markup extension tạo binding cho WPF. MainViewModel/NodeRow nhận service để định dạng lại mà không thay collection. Không đăng ký sự kiện trên từng node của cây lớn.
- Giữ MIT/LTA, icon Windows, recycle-only, ảo hóa và Release một EXE + LICENSE, không PDB. Phiên bản mới 1.2.0.

Các cách đã cân nhắc: đổi culture rồi khởi động lại đơn giản nhưng không đáp ứng cập nhật ngay; hai cửa sổ/giao diện riêng gây trùng lặp. Chọn catalog nhúng + binding notification để đổi trong cửa sổ hiện có và kiểm tra được bằng service độc lập.
