# 🪄 AR Magic Touch - Retro Pixel Art

![Status](https://img.shields.io/badge/Status-In_Development-brightgreen)
![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Mac-lightgrey)
![Unity](https://img.shields.io/badge/Unity-2022.3%2B-black?logo=unity)
![Python](https://img.shields.io/badge/Python-3.9%2B-blue?logo=python)

**AR Magic Touch** là một tựa game tương tác thực tế ảo (AR) phong cách đồ họa Retro Pixel-Art (16-bit). Người chơi sẽ hóa thân thành một pháp sư bảo vệ lâu đài, sử dụng chính đôi bàn tay thực tế của mình để vẽ các ấn chú ma thuật trong không trung, phá vỡ bóng bay và tiêu diệt những kẻ địch đang rơi từ trên trời xuống.

---

## 🚀 Các Tính Năng & Công Nghệ Nổi Bật

Dự án này là sự kết hợp hoàn hảo giữa Game Engine, Trí tuệ nhân tạo (AI) và Thị giác máy tính (Computer Vision):

- **Hand Tracking mượt mà 60+ FPS:** Sử dụng Python & MediaPipe chạy ngầm (Headless Mode) kết hợp với giao thức mạng UDP Đa luồng (Multi-threading) trong Unity để đảm bảo hiệu năng game không bị tụt FPS khi xử lý camera.
- **Dual-Threshold Hysteresis:** Thuật toán chống rung (Schmitt Trigger) giúp việc chụm ngón tay (Pinch) cực kỳ ổn định, không bị đứt gãy nét vẽ khi tay hơi rung lắc nhẹ.
- **Thuật toán Coyote Time:** Đệm thời gian 0.08 giây giúp tự động vá lại nét vẽ khi người chơi vung tay quá nhanh khiến camera bị nhòe (Motion Blur).
- **Universal Path-Based Snapping:** Hỗ trợ vẽ các ký tự đa nét phức tạp (như E, A, X). Tự động "hút" nam châm các nét vẽ rời rạc lại với nhau để tạo thành hình khối hoàn chỉnh cho AI chấm điểm.
- **Nhận diện Ấn chú bằng AI (Unity Sentis):** Tích hợp mô hình Mạng Nơ-ron (ONNX Model) trực tiếp vào Unity để nhận diện hình vẽ của người chơi với độ chính xác cao mà không cần gọi API qua mạng.
- **Đồ họa Retro Pixel-Art:** Hệ thống UI tự động cắt (Auto-Slice), Particle VFX rực rỡ, hiệu ứng đường cong vẽ mượt mà (Catmull-Rom Spline/SMA) như mực neon.
- **Hỗ trợ 2 Người chơi (Local Co-op):** Hệ thống có thể theo dõi độc lập Tay 1 (Player 1) và Tay 2 (Player 2) trên cùng một màn hình.

---

## 📂 Cấu Trúc Hệ Thống Cốt Lõi

- **`HandTrackingBridge.cs`:** Quản lý UDP Socket, Đa luồng nhận dữ liệu, Lọc nhiễu tọa độ và Logic chụm tay (Hysteresis).
- **`DrawingManager.cs`:** Quản lý thao tác vẽ (LineRenderer), Coyote Time, Snapping và gửi dữ liệu điểm vẽ đi nhận diện.
- **`GameManager.cs` & `UIManager.cs`:** Trái tim của game. Quản lý vòng lặp trạng thái (Waiting for Camera -> Playing -> Game Over), hiển thị máu (Hearts), Combo thả nổi và điểm số.
- **`EnemySpawner.cs` & `EnemyBalloon.cs`:** Quản lý quái vật (Knight, Gargoyle, Golem) và gán các quả bóng bay mang ký tự ngẫu nhiên.
- **`mediapipe_streamer.py`:** Ứng dụng Python đọc Webcam và trích xuất tọa độ xương bàn tay gửi cho Unity (chế độ ẩn/tối ưu CPU).

---

## 🛠️ Hướng Dẫn Cài Đặt (Setup)

### 1. Yêu cầu hệ thống (Prerequisites)

- Unity Editor (khuyên dùng bản 2022.3 LTS trở lên).
- Python 3.9+ đã được cài đặt trên máy.
- Webcam (cần thiết để bắt cử chỉ tay).

### 2. Cài đặt môi trường Python

Mở Terminal/Command Prompt và cài đặt các thư viện cần thiết cho hệ thống Hand Tracking:

```bash
pip install opencv-python mediapipe
```

### 3. Clone Dự án

```bash
git clone https://github.com/Daitran18031005/MagicTouchAR.git
```

Sau đó mở thư mục dự án vừa tải về bằng Unity Hub.

---

## 🎮 Hướng Dẫn Chạy & Cách Chơi

### Khởi động Game

1. Mở Scene chính của game (ví dụ: `SampleScene` hoặc `MainScene`).
2. Bấm nút **Play** trong Unity.
3. Màn hình sẽ hiển thị trạng thái *"Waiting for Camera Setup..."*.
4. Quá trình Python Streamer sẽ được Unity tự động gọi chạy ngầm. Sau khoảng 2-3 giây, khi camera bắt được chuyển động tay của bạn, game sẽ tự động bắt đầu và quái vật sẽ rơi xuống!

> **Lưu ý:** Nếu muốn xem màn hình Camera Python để debug, hãy mở file `mediapipe_streamer.py` và đổi biến `ENABLE_PREVIEW = True`.

### Cách chơi

- **Vị trí:** Đứng trước Webcam (cách khoảng 0.5m - 1m), đảm bảo phòng có đủ ánh sáng.
- **Bắt đầu vẽ:** Đưa tay lên, chụm ngón trỏ và ngón cái lại với nhau. Nét vẽ ma thuật sẽ xuất hiện trên màn hình bám theo ngón tay của bạn.
- **Kết thúc nét vẽ:** Mở ngón tay ra.
- **Nối nét (các chữ như X, V, E):** Sau khi mở tay để vẽ xong nét 1, bạn có 0.5 giây để chụm tay lại và vẽ tiếp nét 2. Hệ thống sẽ tự động hút nét vẽ của bạn cho dính vào nhau. Quá 0.5 giây không vẽ thêm, game sẽ tự đem hình đi nhận diện.
- **Mục tiêu:** Hãy nhìn ký tự trên quả bóng bay của kẻ địch đang rơi. Vẽ ký hiệu tương tự lên không trung để làm nổ bóng. Nếu kẻ địch chạm đất, bạn sẽ mất 1 Trái tim (Mạng).

---

## 🧑‍💻 Đóng Góp (Contributing)

Đây là một dự án cá nhân/học thuật. Mọi ý kiến đóng góp, báo lỗi (Issues) hoặc Pull Requests đều được chào đón!

**Bản quyền đồ họa:** Các tài nguyên đồ họa Pixel Art trong dự án được tạo bằng AI (DALL-E/Midjourney) và đã được tự động cắt (Auto-Slice) thông qua công cụ Editor script nội bộ.