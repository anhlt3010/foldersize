# Thiết kế FolderSize cho Windows

Ngày: 2026-10-27. Trạng thái: chờ người dùng duyệt.

## Mục đích và yêu cầu đã xác nhận

Ứng dụng desktop Windows giúp tìm nhanh những file và thư mục chiếm nhiều dung lượng để dọn ổ. Người dùng chọn đường dẫn thư mục hoặc ổ đĩa, quét toàn bộ nội dung, duyệt theo cây thư mục và xóa trực tiếp các mục đã chọn. Giao diện dễ dùng, trực quan. Người dùng đã chọn C# + .NET + WPF và đồng ý bố cục trong bản phác thảo layout.html.

## Các hướng đã xem xét

- C# + .NET + WPF: phù hợp Windows, tích hợp hệ thống file và Thùng rác thuận tiện; giao diện sử dụng XAML. Đây là hướng người dùng chọn theo đề xuất.
- Python + PySide6: dễ viết và có bộ thành phần Qt đầy đủ; cần đóng gói Python và Qt khi phát hành. Không chọn vì ứng dụng chỉ dành cho Windows và cần tích hợp hệ thống ổn định.

## Phạm vi phiên bản đầu

Chọn thư mục bằng hộp thoại hoặc nhập đường dẫn tuyệt đối, bao gồm đường dẫn gốc ổ đĩa. Quét đệ quy, tính tổng dung lượng file trong từng thư mục và tổng số file/thư mục. Hiển thị cây thống nhất chứa cả file lẫn thư mục, sắp xếp dung lượng giảm dần giữa các mục cùng cha. Mở/thu gọn từng nhánh, chọn một mục để xem thông tin, mở vị trí trong Explorer, chuyển mục vào Thùng rác và quét lại.

Không làm trong phiên bản đầu: tìm file trùng, tự động dọn rác, gợi ý xóa bằng AI, biểu đồ treemap, lịch quét, xuất báo cáo, chọn nhiều mục để xóa, theo dõi thay đổi liên tục, quét NTFS bằng MFT. Không tự quyết định nội dung nào nên xóa.

## Giao diện

Bố cục đã được người dùng đồng ý: thanh đường dẫn, nút Chọn thư mục và Quét/Quét lại phía trên; tổng dung lượng đã quét, số file, số thư mục và trạng thái ngay dưới. Bảng cây ở bên trái gồm Tên, Dung lượng, % thư mục cha kèm thanh tỷ lệ và Số file. Panel bên phải hiển thị tên, đường dẫn, dung lượng, Mở trong Explorer và Chuyển vào Thùng rác. Thanh trạng thái dưới cùng hiển thị tiến độ hoặc kết quả và số mục bị bỏ qua, có chi tiết lỗi.

Đề xuất của người xây dựng: giao diện tiếng Việt, nền sáng, hỗ trợ co giãn cửa sổ và thao tác bàn phím cơ bản. Khởi đầu hiện trạng thái trống hướng dẫn chọn vị trí. Trong lúc quét, hiển thị kết quả tạm đã thu thập, ghi rõ chưa hoàn tất; không sắp xếp lại liên tục gây nhảy dòng. Có nút Hủy; không cho đổi vùng quét hoặc xóa trong lúc quét. Khi hủy giữ kết quả tạm và vô hiệu hóa xóa đến khi quét hoàn tất. Sau khi hoàn tất mới sắp xếp các cấp theo dung lượng.

## Quét và ý nghĩa dung lượng

Các quyết định dưới đây là đề xuất kỹ thuật của người xây dựng, chưa phải yêu cầu riêng của người dùng:

- Mục tiêu Windows 10/11 x64. Chọn phiên bản .NET LTS còn được hỗ trợ và thư viện WPF phù hợp lúc lập kế hoạch.
- Dung lượng là tổng kích thước logic của file theo byte, không phải dung lượng thực chiếm trên ổ. Hiển thị rõ loại dung lượng, dùng KiB/MiB/GiB theo hệ 1024 và định dạng số theo tiếng Việt. Không cam kết số này trùng phần Used của ổ đĩa.
- Không đi theo directory reparse point, junction hoặc symbolic link để tránh vòng lặp và vượt phạm vi. Hiển thị mục bị bỏ qua kèm lý do; file reparse point cũng bỏ qua tính dung lượng. Không tải nội dung file đám mây để tính kích thước. Hard link được tính theo mỗi đường dẫn, có ghi chú giới hạn trong trợ giúp.
- Bỏ qua lỗi riêng lẻ như từ chối truy cập, file biến mất hoặc lỗi đọc; tiếp tục quét phần còn lại và ghi đường dẫn, lý do. Các mục không đọc được hiển thị trạng thái chưa đầy đủ; lan truyền trạng thái này lên thư mục cha, không thể hiện như dung lượng 0 đã xác nhận. Không tự yêu cầu quyền quản trị.
- Tỷ lệ tính trên tổng dung lượng đã đọc được của thư mục cha; cha bằng 0 thì hiển thị dấu gạch. Tổng số file/thư mục chỉ tính mục thực sự đã ghi nhận và nêu rõ nếu kết quả chưa đầy đủ.
- Quét chạy ngoài luồng giao diện, có hủy cộng tác; tiến độ bằng số mục đã đọc, dung lượng tạm và đường dẫn đang xử lý, không dùng phần trăm giả khi chưa biết tổng.
- Giới hạn tần suất cập nhật giao diện, không tạo control cho mọi file cùng lúc; cây/bảng phải ảo hóa và chỉ tạo các dòng cần nhìn thấy. Mục tiêu thực dụng là duyệt được hàng trăm nghìn mục mà giao diện vẫn phản hồi. Không hứa thời gian quét cụ thể vì phụ thuộc ổ đĩa và quyền truy cập.
- Kết quả là ảnh chụp tại thời điểm quét; ứng dụng cho phép quét lại khi nội dung thực tế thay đổi. Bản đầu không lưu cơ sở dữ liệu hoặc lịch sử quét.

## Xóa và tính nhất quán

Mặc định dùng Windows Shell chuyển vào Thùng rác, không tự chuyển sang xóa vĩnh viễn. Trước mỗi thao tác, hộp xác nhận hiển thị đường dẫn đầy đủ, dung lượng đã quét và cảnh báo bao gồm toàn bộ nội dung con. Thông báo rõ kích thước có thể đã thay đổi từ lần quét.

Nếu Windows không thể đưa mục vào Thùng rác hoặc thao tác cần xóa vĩnh viễn, hủy thao tác và giải thích. Xóa vĩnh viễn không nằm trong bản đầu. Đây là đề xuất bảo vệ dữ liệu cần người dùng duyệt cùng bản thiết kế.

Chỉ xóa một mục được chọn, sau khi quét hoàn tất. Cấm xóa gốc vùng quét và gốc ổ đĩa; người dùng có thể quét thư mục cha để quản lý một thư mục thông thường. Kiểm tra lại mục vẫn nằm trong vùng quét, không phải reparse point và tồn tại trước khi gọi Shell; không tự vượt quyền hoặc thay đổi quyền truy cập. Shell phải chạy ở chế độ chỉ recycle, không cho phép fallback xóa vĩnh viễn.

Trong lúc xóa vô hiệu hóa thao tác xóa/quét khác, hiển thị trạng thái và cho giao diện tiếp tục phản hồi. Chỉ cập nhật cây sau khi có kết quả từ Shell; không suy đoán thành công. Sau thao tác thành công hoặc một phần, quét lại vùng hiện tại để cập nhật tổng; nếu hủy hoặc thất bại, báo rõ và giữ dữ liệu với chỉ báo cần quét lại khi trạng thái thực tế chưa chắc chắn. Không khẳng định chuyển vào Thùng rác đã giải phóng dung lượng ổ đĩa; phần trợ giúp nói rõ phải dọn Thùng rác mới có thể thu hồi chỗ.

## Kiến trúc và phát hành

WPF theo MVVM ở mức vừa đủ: lớp dịch vụ quét file, mô hình cây kết quả, ViewModel điều phối trạng thái và lựa chọn, dịch vụ Shell riêng cho Explorer/Thùng rác, view XAML. Giao diện không trực tiếp quét đệ quy hoặc gọi xóa. Tránh phụ thuộc không cần thiết; người xây dựng chọn thư viện giao diện/cây, cách ảo hóa, điều phối luồng và API Shell cụ thể trong kế hoạch.

Phát hành bản portable self-contained cho Windows x64, chạy được không cần cài .NET riêng. Người xây dựng xác minh toolchain sẵn có trước khi chọn cách build. Không yêu cầu installer, ký số hoặc tự cập nhật trong bản đầu. Ứng dụng chỉ xử lý dữ liệu cục bộ, không gửi đường dẫn hay nội dung file lên mạng.

## Tiêu chí nghiệm thu

1. Chọn hoặc nhập vị trí hợp lệ và thấy toàn bộ cấu trúc đọc được, tổng dung lượng chính xác theo kích thước logic trên bộ dữ liệu kiểm thử biết trước.
2. Cây mở/thu gọn được, cả file và thư mục đều hiện, các mục cùng cha được xếp giảm dần; tỷ lệ và tổng cha nhất quán.
3. Quét/hủy không làm treo giao diện; lỗi quyền truy cập và liên kết vòng không làm dừng hoặc lặp vô hạn; kết quả thiếu được đánh dấu.
4. Xóa yêu cầu xác nhận, đưa dữ liệu thử vào Thùng rác và cập nhật kết quả; hủy, lỗi và mục không recycle được không gây xóa vĩnh viễn.
5. Mở Explorer đúng vị trí; đường dẫn không hợp lệ và mục đã biến mất có thông báo dễ hiểu.
6. Build thành công và kiểm tra trực quan trên Windows, bao gồm trạng thái trống, đang quét, hủy, hoàn tất, lỗi và xác nhận xóa. Kiểm thử xóa chỉ dùng dữ liệu tạm được tạo cho mục đích kiểm thử.

## Điểm cần duyệt

Người dùng duyệt toàn bộ bản thiết kế, đặc biệt các mặc định do người xây dựng đề xuất: Windows 10/11 x64, tiếng Việt/nền sáng, portable, dung lượng logic, bỏ qua liên kết, chỉ xóa qua Thùng rác và không xóa gốc vùng quét.
