# 🪄 AR Magic Touch - Retro Pixel Art

![Status](https://img.shields.io/badge/Status-In_Development-brightgreen)
![Platform](https://img.shields.io/badge/Platform-Windows-lightgrey)
![Unity](https://img.shields.io/badge/Unity-6000.5.6f1-black?logo=unity)
![Python](https://img.shields.io/badge/Python-ch%C6%B0a_ghim_phi%C3%AAn_b%E1%BA%A3n-blue?logo=python)

**AR Magic Touch** là một tựa game tương tác thực tế ảo (AR) phong cách đồ họa Retro Pixel-Art (16-bit). Người chơi sẽ hóa thân thành một pháp sư bảo vệ lâu đài, sử dụng chính đôi bàn tay thực tế của mình để vẽ các ấn chú ma thuật trong không trung, phá vỡ bóng bay và tiêu diệt những kẻ địch đang rơi từ trên trời xuống.

- **Cách chơi:** mỗi quả bóng mang một ký hiệu. Vẽ đúng ký hiệu đó (chụm ngón cái + ngón trỏ trước webcam, hoặc giữ chuột trái ở chế độ Mouse Simulation) thì bóng sẽ nổ. Có 10 ký hiệu: **V, ^, —, |, O, Z, W, S, E, X** (`EnemySpawner._availableSymbols` trong scene, khớp 10 lớp của model ONNX).
- **Chế độ chơi:** **Solo** (3 mạng, kẻ địch chạm đất thì mất 1 mạng) và **Versus** (2 người, chia đôi màn hình, đấu giờ 3/5/7 phút) — `GameMode { Solo, Versus }` trong `Assets/Scripts/Gameplay/VersusMatch.cs`.
- **3 map:** mới là kế hoạch. Hiện chỉ có map lâu đài (`CastleBackground`). Ảnh nền `ClockTowerBackground`, `HalloweenBackground` và nhạc `CombatClockMusic` đã có trong `Assets/` nhưng **chưa được dùng** (xem mục *Kế Hoạch Tiếp Theo* bên dưới).

---

## 📊 Trạng Thái Hiện Tại

| Thành phần | Trạng thái | Ghi chú |
|---|---|---|
| Chơi bằng chuột (Mouse Simulation) | ⚠️ Chưa kiểm tra Play | Code biên dịch sạch. 3 bộ kiểm tra trong Editor đều PASS: `Validate UI Bindings`, `Validate Interaction Repair`, `Validate Drawing Repair`. Chưa có kết quả Play thử (xem [CLEANUP_REPORT](Related/Docs/CLEANUP_REPORT.md)) |
| AR hand tracking bằng webcam | ❌ **Hiện chưa chạy** trên máy gốc | Unity gọi `pythonw` của hệ thống (Python 3.14, thiếu `numpy`), không dùng `.venv` hay conda (`HandTrackingBridge.cs:51,293`). `.venv` đang hỏng sau khi chạy lại `setup_python_env.bat`. Môi trường conda `magictouch` chạy streamer thành công khi chạy riêng (chưa kiểm tra end-to-end với Unity). Việc sửa nằm ở giai đoạn sau |
| Android / mobile | ❌ Chưa làm | Python tracker chỉ chạy trên PC: Unity khởi chạy `pythonw` bằng `Process.Start`. `NativeMediaPipeBridge.cs` là khung dự kiến cho mobile, không có trong scene |
| macOS | ❓ Chưa hỗ trợ chính thức | Chưa kiểm tra. Launcher dùng `pythonw` và file `.bat`, chỉ phù hợp với Windows |

---

## 🚀 Các Tính Năng & Công Nghệ Nổi Bật

Dự án này là sự kết hợp hoàn hảo giữa Game Engine, Trí tuệ nhân tạo (AI) và Thị giác máy tính (Computer Vision):

- **Hand Tracking bằng Python & MediaPipe chạy ngầm (Headless Mode):** gửi dữ liệu sang Unity qua UDP. Unity nhận bằng một luồng nền riêng (multi-threading) nên không chặn main thread. FPS thực đo: khoảng 10–30 FPS tùy camera/backend, không phải 60 FPS (xem [fps-validation.md](Related/Docs/fps-validation.md)).
- **Dual-Threshold Hysteresis:** thuật toán chống rung (Schmitt Trigger) giúp việc chụm ngón tay (Pinch) ổn định. Ngưỡng vào `0.04` / ra `0.08` (`HandTrackingBridge.cs:39,42`). Một mẫu mở tay chưa rõ ràng chỉ tạm dừng nét; mẫu kế tiếp mới xác nhận nhả tay (`PinchTransitionFilter.cs`).
- **Chờ khi mất tracking (thay cho "Coyote Time 0.08 s" cũ):** nếu tay mất dấu tối đa **0,14 giây** (`DrawingManager.trackingLossGrace`) mà tay hiện lại gần chỗ cũ thì nét vẽ được nối tiếp.
- **Bộ lọc vị trí One Euro:** lọc tương đối theo cổ tay để giảm rung đầu ngón tay mà không làm trễ chuyển động cả bàn tay (`OneEuroPositionFilter.cs`, `HandPositionFilter.cs`).
- **Ký hiệu nhiều nét (X, E):** mở tay giữa các nét; hệ thống chờ nét tiếp theo khoảng 0,35 giây (`multiStrokeDelay`, giới hạn trong 0,20–0,45 s) rồi mới chấm. Chữ X và E chỉ được chấm sớm khi đủ hình (`GestureCompletionPolicy.cs`).
- **Nhận diện Ấn chú bằng AI (Unity Inference Engine / Sentis, gói `com.unity.ai.inference`):** model CNN `Assets/Models/magic_touch_model.onnx` chạy ngay trong Unity, không gọi API qua mạng. Nếu model không nạp được thì dùng bộ nhận diện hình học `$P` (`PRecognizer.cs`) thay thế.
- **Đồ họa Retro Pixel-Art:** UI Phase 9 (font PressStart2P, 9-slice), Particle VFX, nét vẽ mượt bằng Catmull-Rom Spline/SMA như mực neon.
- **2 người chơi (Versus):** theo dõi độc lập Tay 1 và Tay 2. Điểm được tính cho người chơi ở nửa màn hình nơi nét vẽ bắt đầu (`VersusMatch.PlayerForScreenX`).

---

## 📂 Cấu Trúc Hệ Thống Cốt Lõi

- **`mediapipe_streamer.py`** (`Tools/`): đọc webcam, chạy MediaPipe Hand Landmarker, gửi tọa độ bàn tay (UDP 9050) và ảnh camera JPEG (UDP 9051) cho Unity.
- **`HandTrackingBridge.cs`:** tự khởi chạy Python streamer, nhận UDP bằng luồng nền, lọc nhiễu tọa độ (One Euro) và xử lý logic chụm tay (Hysteresis). Có cả chế độ giả lập bằng chuột.
- **`MediaPipeUDPReceiver.cs`:** nhận luồng video (UDP 9051) để vẽ nền camera. Không mở thêm luồng nhận landmark nếu `HandTrackingBridge` đã nhận.
- **`DrawingManager.cs`:** quản lý thao tác vẽ (LineRenderer), chờ khi mất tracking, nét nhiều nét và gửi điểm vẽ đi nhận diện.
- **`SentisRecognizer.cs` / `PRecognizer.cs`:** nhận diện ký hiệu (Sentis ONNX là mặc định, `$P` là dự phòng).
- **`GameManager.cs` & `UIManager.cs` & `MenuManager.cs`:** Trái tim của game. Quản lý vòng lặp trạng thái (Main Menu → Waiting for Camera → Playing → Paused/Game Over), hiển thị máu (Hearts), Combo và điểm số, chế độ Versus.
- **`EnemySpawner.cs` & `EnemyBalloon.cs`:** sinh kẻ địch (scene đang gán 3 sprite `Bat`, `Knight`, `Robot` trong `Assets/Sprites/Enemies/`) treo trên bóng bay mang ký hiệu ngẫu nhiên.

### Cấu trúc thư mục

```text
MagicTouch/
├── Assets/            # Game Unity: Scenes/SampleScene.unity, Scripts/, Prefabs/, Models/ (ONNX),
│                      #   Sprites/, Audio/, UI/ (Phase 9 + MenuManager), Editor/ (công cụ & kiểm tra)
├── Tools/             # Core Python: mediapipe_streamer.py, *.task, *.bat, requirements.txt.
│                      #   Tên và vị trí thư mục này bị GÁN CỨNG trong code — không đổi tên/dời.
├── Related/           # Tài liệu không cần khi chạy game: Docs/, Design/, Reference-Art/, Logic-Notes/
│                      #   (Data-Samples/ và _ToDelete/ chỉ có trên máy, bị .gitignore)
├── Train/             # train_magic_touch.py: huấn luyện CNN và xuất magic_touch_model.onnx (cần torch, torchvision)
├── hand_landmarker.task   # Model MediaPipe gốc (streamer tìm ở thư mục gốc trước, rồi tới Tools/)
├── Packages/, ProjectSettings/   # Cấu hình Unity
└── README.md
```

### Sơ đồ pipeline (chế độ AR)

```text
 Webcam
   │  OpenCV: CAP_DSHOW trước, lỗi thì CAP_MSMF (Windows), camera 0, 640x480
   ▼
 Tools/mediapipe_streamer.py ── MediaPipe HandLandmarker (hand_landmarker.task, đọc qua buffer)
   │  JSON landmark ──► UDP 127.0.0.1:9050      JPEG (--stream-video) ──► UDP 127.0.0.1:9051
   ▼                                              ▼
 HandTrackingBridge (luồng nền nhận 9050)      MediaPipeUDPReceiver (luồng nền nhận 9051)
   │  lọc session-id (TrackingStreamSession)        └─► RawImage "CameraBackground" (nền camera)
   │  lọc One Euro / palm-relative, chống spike
   │  PinchTransitionFilter (vào 0.04 / ra 0.08)
   ▼
 DrawingManager ── vẽ LineRenderer, chờ mất tracking 0.14 s, chờ nét kế 0.35 s
   ▼
 RecognizePoints: SentisRecognizer (ONNX, mặc định) │ dự phòng: PRecognizer ($P)
   ▼  OnGestureRecognized
 GameManager.HandleGestureRecognized ─► OnSymbolRecognized(symbol)
   ▼
 EnemyBalloon.Pop()  (nổ mọi bóng trùng ký hiệu; điểm combo 10/30/60…)
```

Chế độ Mouse Simulation bỏ qua toàn bộ phần Python/UDP: `HandTrackingBridge` và `DrawingManager` đọc chuột trực tiếp, và nền camera dùng `WebCamTexture` của Unity.

---

## 🛠️ Yêu Cầu Hệ Thống

- **Unity Editor 6000.5.6f1** (Unity 6, đọc từ `ProjectSettings/ProjectVersion.txt`). Phiên bản 2022.3 trong README cũ không còn đúng.
- **Windows** (macOS chưa hỗ trợ chính thức).
- **Webcam** (chỉ cần cho chế độ AR).
- **Python** (chỉ cần cho chế độ AR): phiên bản phù hợp **chưa được ghim**, sẽ ghim ở giai đoạn portable setup. `Tools/requirements.txt` hiện chỉ ghi phiên bản tối thiểu: `opencv-python>=4.10.0`, `mediapipe>=0.10.0`, `numpy>=1.24.0`.

---

## 🎮 Cách Chạy Toàn Bộ Pipeline (trạng thái hiện tại, trước khi sửa lỗi AR)

### A. Chạy bằng chuột (không cần Python)

1. Clone dự án:
   ```bash
   git clone https://github.com/Daitran18031005/MagicTouchAR.git
   ```
2. Mở thư mục vừa clone bằng Unity Hub với **Unity 6000.5.6f1**.
3. Mở scene `Assets/Scenes/SampleScene.unity`.
4. Trên thanh menu chọn `Tools > Magic Touch > Switch Input > Mouse Simulation (PC Debug)`.
   > ⚠️ Lệnh này **tự lưu scene** (`MagicTouchSceneSetup.cs:457-488`). Muốn quay lại AR thì chọn `Tools > Magic Touch > Switch Input > AR Hand Tracking (MediaPipe)`. Bản scene trong repo mặc định là chế độ AR.
5. Bấm **Play**, chọn **PLAY** ở menu chính, rồi giữ chuột trái và kéo để vẽ ký hiệu. Nửa trái màn hình là Tay 1, nửa phải là Tay 2.

### B. Chạy chế độ AR (cần Python có đủ thư viện)

> ⚠️ **Chế độ này CHƯA hoạt động với cấu hình mặc định.** Khi bấm Play, Unity tự gọi `pythonw` có trên PATH (`HandTrackingBridge.cs:293`; nếu lỗi thì thử `python`, dòng 310). Nếu Python đó thiếu thư viện, streamer sẽ chết ngầm và game đứng mãi ở *"Waiting for Camera Setup..."*. Script `Tools/setup_python_env.bat` ghi rằng game "sẽ tự dùng .venv", nhưng **không có code nào dùng `.venv`**.

**Cách đã chạy được streamer** là dùng môi trường conda **`magictouch`**. Đã kiểm tra trên máy gốc: Python 3.11.17, opencv-python 5.0.0.93 (và opencv-contrib-python 5.0.0.93), mediapipe 1.1.1, numpy 2.4.6. Môi trường này **chưa ghim vào repo**. Streamer chạy riêng với môi trường này mở được camera và chạy ổn; **chưa kiểm tra end-to-end với Unity**.

Các bước chạy tay (theo các script hiện có):

1. Tạo môi trường Python có đủ thư viện. Hoặc dùng `magictouch` như trên, hoặc chạy `Tools\setup_python_env.bat`: script này tạo `.venv` bằng `python` trên PATH rồi `pip install -r Tools\requirements.txt`. Nếu Python trên PATH là bản quá mới, cài đặt có thể thiếu gói. Đừng chạy lại script này đè lên một `.venv` cũ tạo bằng phiên bản Python khác.
2. Trong Unity, chọn `Tools > Magic Touch > Switch Input > AR Hand Tracking (MediaPipe)` (lệnh này tự lưu scene).
3. **Tắt tự khởi chạy Python** khi chạy tay. Trên GameObject `HandTrackingBridge`, bỏ chọn *Auto Launch Python Streamer* (component `HandTrackingBridge`) và *Auto Launch Python On Play* (component `MediaPipeUDPReceiver`). Lý do: khi tự khởi chạy, Unity tạo một session-id và **bỏ qua mọi gói UDP không mang session-id đó** (`TrackingStreamSession.cs`), nên streamer chạy tay sẽ bị lờ đi.
4. Mở terminal ở thư mục gốc project, kích hoạt môi trường rồi chạy tracker (bật tracker **trước** khi bấm Play):
   ```bat
   conda activate magictouch
   Tools\run_hand_tracker.bat
   ```
   Script gọi `python Tools\mediapipe_streamer.py --stream-video`. Muốn xem cửa sổ camera để debug thì chạy `Tools\run_hand_tracker_with_preview.bat` (thêm `--preview`), hoặc đổi `ENABLE_PREVIEW = True` trong `Tools/mediapipe_streamer.py`.
5. Kiểm tra tracker đang chạy:
   - Terminal in `Camera active: ...`, rồi cứ 2 giây in một dòng `[STREAMER] Camera: … FPS | Tracking: … FPS | … | Hands: N`.
   - Nếu dùng bản preview: có cửa sổ **"MediaPipe AR Hand Tracker"**.
   - File `Logs/tracking-performance.json` được cập nhật mỗi 2 giây.
   - Tracker gửi tới Unity qua UDP **9050** (landmark) và **9051** (video). Unity là bên lắng nghe 2 cổng này khi đang Play.
6. Bấm **Play** trong Unity và chọn **PLAY**. Khi Unity nhận được gói landmark đầu tiên, màn *"Waiting for Camera Setup..."* sẽ tự tắt. Trong Inspector, `HandTrackingBridge > Measured Camera Performance` hiện FPS đo được.

Một cách khác (**chưa kiểm tra**): giữ chế độ tự khởi chạy và đặt trường *Python Executable* của `HandTrackingBridge` thành `pythonw.exe` nằm trong môi trường `magictouch` (`<thư-mục-conda>\envs\magictouch\pythonw.exe`).

### Cách chơi

- **Vị trí:** Đứng trước Webcam (cách khoảng 0.5m - 1m), đảm bảo phòng có đủ ánh sáng.
- **Bắt đầu vẽ:** Đưa tay lên, chụm ngón trỏ và ngón cái lại với nhau. Nét vẽ ma thuật sẽ xuất hiện trên màn hình bám theo ngón tay của bạn.
- **Kết thúc nét vẽ:** Mở ngón tay ra.
- **Nối nét (các chữ như X, E):** sau khi mở tay để vẽ xong nét 1, bạn có khoảng **0,35 giây** để chụm tay lại và vẽ tiếp nét 2. Quá thời gian đó không vẽ thêm, game sẽ đem hình đi nhận diện.
- **Mục tiêu:** Hãy nhìn ký tự trên quả bóng bay của kẻ địch đang rơi. Vẽ ký hiệu tương tự lên không trung để làm nổ bóng. Ở chế độ Solo, nếu kẻ địch chạm đất, bạn sẽ mất 1 Trái tim (Mạng).
- **Tạm dừng:** phím **Esc**.

---

## 🩹 Lỗi Thường Gặp (đã gặp thật)

| Hiện tượng | Nguyên nhân | Cách xử lý |
|---|---|---|
| Đứng mãi ở *"Waiting for Camera Setup..."*, không có lỗi trong Console | Unity gọi `pythonw` trên PATH (máy gốc: Python 3.14 thiếu `numpy`). Process chạy ẩn nên không hiện lỗi | Chạy tracker bằng tay trong môi trường có đủ thư viện (mục B). Bản sửa chính thức để sau |
| `.venv` báo `No module named 'numpy._core._multiarray_umath'` | Chạy lại `setup_python_env.bat` đè lên `.venv` cũ tạo bằng Python khác, nên thư viện và trình thông dịch lệch phiên bản | Xóa `.venv` rồi tạo lại bằng đúng một phiên bản Python. Chưa ghim phiên bản |
| Mở webcam treo 38–43 giây | Backend `CAP_MSMF` dò thiết bị rất chậm trên một số webcam Windows | Đã xử lý: streamer thử `CAP_DSHOW` trước (`mediapipe_streamer.py:61-64`). Đánh đổi: DSHOW đo được ~15 FPS, MSMF ~30 FPS |
| MediaPipe crash (`abort()`) khi đường dẫn project có dấu tiếng Việt | Đường dẫn Unicode truyền vào tầng C++ của MediaPipe | Đã xử lý: đọc model bằng Python rồi truyền qua `model_asset_buffer` (`mediapipe_streamer.py:193-195`) |

---

## 🗺️ Kế Hoạch Tiếp Theo (CHƯA LÀM)

- **Portable setup:** `.venv` với phiên bản Python và thư viện được ghim, file `.env` cấu hình. Unity chọn đúng Python thay vì `pythonw` trên PATH.
- **Bản Android:** chơi bằng cảm ứng (Python tracker không chạy trên mobile).
- **Animation và hệ thống 3 map** (lâu đài, tháp đồng hồ, Halloween).

Chi tiết dọn dẹp project và các việc còn tồn: [Related/Docs/CLEANUP_REPORT.md](Related/Docs/CLEANUP_REPORT.md).

---

## 🧑‍💻 Đóng Góp (Contributing)

Đây là một dự án cá nhân/học thuật. Mọi ý kiến đóng góp, báo lỗi (Issues) hoặc Pull Requests đều được chào đón!

**Bản quyền đồ họa:** Các tài nguyên đồ họa Pixel Art trong dự án được tạo bằng AI (DALL-E/Midjourney) và đã được tự động cắt (Auto-Slice) thông qua công cụ Editor script nội bộ. Font PressStart2P đi kèm giấy phép OFL (`Assets/UI/Phase9/OFL.txt`).
