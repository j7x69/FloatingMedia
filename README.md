# 🎵 Floating Media Studio

Một tiện ích điều khiển nhạc và video dạng cửa sổ nổi siêu nhẹ, sang trọng dành cho Windows. Ứng dụng có khả năng **chạy đè lên mọi game và ứng dụng full-screen mà không bao giờ làm mất tiêu điểm chuột hay văng game**.

Giao diện kính mờ hiện đại, **tự động đổi màu theo ảnh bìa bài hát**, tích hợp thanh thời lượng bài hát thời gian thực có thể kéo tua nhạc tự do và cơ chế co giãn kích thước linh hoạt 8 hướng.

![Hệ điều hành](https://img.shields.io/badge/H%E1%BB%87%20%C4%91i%E1%BB%81u%20h%C3%A0nh-Windows%2010%20%7C%2011-blue?style=flat-square)
![Giấy phép](https://img.shields.io/badge/Gi%E1%BA%A5y%20ph%C3%A9p-MIT-green?style=flat-square)
![Cài đặt](https://img.shields.io/badge/C%C3%A0i%20%C4%91%E1%BA%B7t-Kh%C3%B4ng%20c%E1%BA%A7n%20(Ch%E1%BA%A1y%20ngay)-orange?style=flat-square)
![Dung lượng](https://img.shields.io/badge/Dung%20l%C6%B0%E1%BB%A3ng-~45%20KB-purple?style=flat-square)

---

## ✨ Tính năng nổi bật

- **🛡️ Chạy đè lên Game & Full-screen (`WS_EX_NOACTIVATE`)**:
  - Không bao giờ cướp chuột hay focus của game full-screen, video YouTube (F11) hoặc các ứng dụng đang mở.
  - Bấm đổi nhạc, chỉnh âm lượng thoải mái mà game không bị giật, lag hay thu nhỏ xuống thanh Taskbar.
- **🎨 Tự động đổi màu theo ảnh bìa (Dynamic Color Theming)**:
  - Tự động phân tích và trích xuất dải màu sống động nhất từ ảnh bìa album Spotify hoặc thumbnail video YouTube.
  - Nền kính mờ, thanh thời lượng, con trỏ và nút bấm sẽ đồng bộ màu sắc hài hòa với bài hát đang phát.
- **⏱️ Đồng bộ thời gian thực & Kéo tua nhạc (Interactive Timeline)**:
  - Hiển thị thời gian phát chính xác từng giây (`01:23 / 03:45`) đồng bộ trực tiếp từ Windows Media.
  - Bấm hoặc kéo rê con trỏ viên thuốc trên thanh thời lượng để tua đến đúng đoạn bạn muốn nghe.
- **📐 Kéo giãn kích thước 8 hướng chuẩn Windows & Tự thích ứng**:
  - Nắm bất kỳ cạnh nào (Trên, Dưới, Trái, Phải) hoặc 4 góc để kéo to nhỏ tự do.
  - **Chế độ Thẻ (Card)**: Ảnh bìa lớn ở trên, tiêu đề, ca sĩ, thanh thời lượng và đầy đủ cụm phím điều khiển.
  - **Chế độ Thanh ngang (Bar)**: Thanh mini nằm ngang gọn gàng.
  - **Chế độ Bong bóng (Micro, < 135px)**: Thu nhỏ tối đa thành đúng 1 ảnh bìa bo tròn + nút Play/Pause kính mờ ở giữa.
- **🌌 Ánh sáng tỏa hào quang (Ambient Glow)**:
  - Vầng hào quang màu sắc tỏa ra mềm mại phía sau ảnh bìa và thẻ bài hát.
- **🧲 Tự động hít mép màn hình (Edge Snapping)**:
  - Tự động căn dính nhẹ nhàng vào mép màn hình khi kéo lại gần (< 18px).
- **📌 Khóa vị trí (`📌` Lock Mode)**:
  - Khóa cố định vị trí và kích thước để bạn thoải mái chơi game mà không sợ lỡ tay kéo nhầm widget.
- **🔒 Chống mở trùng lặp (Single-Instance)**:
  - Quản lý bằng System Mutex, không bao giờ bị mở đúp 2 cửa sổ cùng lúc.

---

## 🚀 Hướng dẫn sử dụng

### Cách 1: Tải về dùng ngay (Khuyên dùng)
1. Tải file `FloatingMediaBar.exe` từ mục [Releases](https://github.com/j7x69/FloatingMediaBar/releases).
2. Nhấp đúp chuột vào file `.exe` để chạy ngay (không cần cài đặt thêm bất kỳ phần mềm nào).

### Cách 2: Tự biên dịch từ mã nguồn
Chỉ cần chạy file `build.bat` có sẵn hoặc chạy lệnh sau trong PowerShell/CMD:
```cmd
build.bat
```

---

## 🎮 Bảng thao tác & Phím tắt

| Thao tác | Chức năng |
| :--- | :--- |
| **Lăn con lăn chuột (Scroll)** | Rê chuột lên widget và lăn con lăn để tăng / giảm âm lượng hệ thống |
| **Kéo chuột bất kỳ vị trí nào** | Di chuyển widget đến mọi vị trí trên màn hình |
| **Nắm cạnh / góc để kéo** | Thay đổi kích thước tự do theo 8 hướng |
| **Kéo thanh thời lượng** | Tua nhanh / lùi bài hát đến giây mong muốn |
| **Chuột phải vào widget** | Mở menu tùy chọn (Độ mờ, Đổi chế độ, Khóa vị trí, Bật/tắt Ambient, Thoát) |
| **Nút `📌` trên thanh** | Khóa / Mở khóa vị trí và kích thước |

---

## 📄 Giấy phép

Dự án được phát hành theo giấy phép [MIT License](LICENSE).
