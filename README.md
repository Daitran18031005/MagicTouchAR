# 🪄 AR Magic Touch - Retro Pixel Art

![Status](https://img.shields.io/badge/Status-In_Development-brightgreen)
![Platform](https://img.shields.io/badge/Platform-Windows-lightgrey)
![Unity](https://img.shields.io/badge/Unity-6000.5.6f1-black?logo=unity)
![Python](https://img.shields.io/badge/Python-3.11-blue?logo=python)

**AR Magic Touch** là một tựa game tương tác thực tế ảo (AR) phong cách đồ họa Retro Pixel-Art (16-bit). Người chơi sẽ hóa thân thành một pháp sư bảo vệ lâu đài, sử dụng chính đôi bàn tay thực tế của mình để vẽ các ấn chú ma thuật trong không trung, phá vỡ bóng bay và tiêu diệt những kẻ địch đang rơi từ trên trời xuống.

- **Cách chơi:** mỗi quả bóng mang một ký hiệu. Vẽ đúng ký hiệu đó (chụm ngón cái + ngón trỏ trước webcam, hoặc giữ chuột trái ở chế độ Mouse Simulation) thì bóng sẽ nổ. Có 10 ký hiệu: **V, ^, —, |, O, Z, W, S, E, X** (`EnemySpawner._availableSymbols` trong scene, khớp 10 lớp của model ONNX).
- **Chế độ chơi:** **Solo** (3 mạng, kẻ địch chạm đất thì mất 1 mạng) và **Versus** (2 người, chia đôi màn hình, đấu giờ 3/5/7 phút) — `GameMode { Solo, Versus }` trong `Assets/Scripts/Gameplay/VersusMatch.cs`.
- **3 map:** mới là kế hoạch. Hiện chỉ có map lâu đài (`CastleBackground`). Ảnh nền `ClockTowerBackground`, `HalloweenBackground` và nhạc `CombatClockMusic` đã có trong `Assets/` nhưng **chưa được dùng** (xem mục *Kế Hoạch Tiếp Theo* bên dưới).

---

## 📊 Trạng Thái Hiện Tại

| Thành phần | Trạng thái | Ghi chú |
|---|---|---|
| Chơi bằng chuột (Mouse Simulation) | ✅ Chạy được | Đã thử trên máy gốc và một máy thứ hai (clone mới): vẽ được, Restart đúng 1 lần mỗi click. Ở chế độ này Unity **không mở Python** |
| AR hand tracking bằng webcam | ✅ **Chạy được qua `.venv`** | Đã thử trên máy gốc và một máy thứ hai (clone mới): `.venv` tạo bằng `Tools\setup_python_env.bat` (Python 3.11), Unity tự khởi chạy streamer **đúng 1 lần** từ `.venv`, tay điều khiển được, vẽ ký hiệu thì bóng nổ. Unity không dùng Python hệ thống |
| Nhận diện ký hiệu | ✅ Chạy được | CNN ONNX trong Unity; ngày 21/10/2026 fine-tune cho `^`. Số liệu chỉ trên dữ liệu tự sinh ([Train/CARET_IMPROVEMENT.md](Train/CARET_IMPROVEMENT.md)); cần thử thêm với người chơi thật |
| Android / mobile | ❌ Chưa làm | Python tracker chỉ chạy trên PC (Editor/Standalone). `NativeMediaPipeBridge.cs` là khung dự kiến cho mobile, không có trong scene |
| macOS | ❓ Chưa hỗ trợ chính thức | Có `Tools/setup_python_env.sh` và Unity tìm `.venv/bin/python3`, nhưng **chưa kiểm thử trên macOS** |

---

## 🚀 Các Tính Năng & Công Nghệ Nổi Bật

Dự án này là sự kết hợp hoàn hảo giữa Game Engine, Trí tuệ nhân tạo (AI) và Thị giác máy tính (Computer Vision):

- **Hand Tracking bằng Python & MediaPipe chạy ngầm (Headless Mode):** gửi dữ liệu sang Unity qua UDP. Unity nhận bằng một luồng nền riêng (multi-threading) nên không chặn main thread. FPS thực đo: khoảng 10–30 FPS tùy camera/backend, không phải 60 FPS (xem [fps-validation.md](Related/Docs/fps-validation.md)).
- **Dual-Threshold Hysteresis:** thuật toán chống rung (Schmitt Trigger) giúp việc chụm ngón tay (Pinch) ổn định. Ngưỡng vào `0.04` / ra `0.08` (`HandTrackingBridge.cs:39,42`). Một mẫu mở tay chưa rõ ràng chỉ tạm dừng nét; mẫu kế tiếp mới xác nhận nhả tay (`PinchTransitionFilter.cs`).
- **Chờ khi mất tracking (thay cho "Coyote Time 0.08 s" cũ):** nếu tay mất dấu tối đa **0,14 giây** (`DrawingManager.trackingLossGrace`) mà tay hiện lại gần chỗ cũ thì nét vẽ được nối tiếp.
- **Bộ lọc vị trí One Euro:** lọc tương đối theo cổ tay để giảm rung đầu ngón tay mà không làm trễ chuyển động cả bàn tay (`OneEuroPositionFilter.cs`, `HandPositionFilter.cs`).
- **Ký hiệu nhiều nét (X, E):** mở tay giữa các nét; hệ thống chờ nét tiếp theo `multiStrokeDelay` giây (giá trị trong scene hiện là **0,5 s**; mặc định trong code là 0,8 s; giá trị bị kẹp trong 0,20–0,8 s) rồi mới chấm. Chữ X và E chỉ được chấm sớm khi đủ hình (`GestureCompletionPolicy.cs`): X phải là hai đoạn thẳng cắt nhau, E phải đủ thân dọc và ba nét ngang, O phải gần khép kín.
- **Nhận diện Ấn chú bằng AI (Unity Inference Engine / Sentis, gói `com.unity.ai.inference`):** model CNN `Assets/Models/magic_touch_model.onnx` chạy ngay trong Unity, không gọi API qua mạng. Nếu model không nạp được thì dùng bộ nhận diện hình học `$P` (`PRecognizer.cs`) thay thế. Ngày 21/10/2026 model được fine-tune lại để nhận ký hiệu `^` (Caret) tốt hơn, giữ nguyên cấu trúc và thứ tự lớp (chi tiết, số đo và cách khôi phục model cũ: [Train/CARET_IMPROVEMENT.md](Train/CARET_IMPROVEMENT.md)). Số đo trong đó là trên dữ liệu **tự sinh**, chưa phải độ chính xác với người chơi thật.
- **Phản hồi theo kết quả chiến đấu:** nét vẽ chỉ xanh và hiện `HIT` khi thật sự làm nổ ít nhất một bóng; nhận đúng ký hiệu nhưng không có bóng trùng thì đỏ và hiện `NO TARGET`; không nhận ra thì `MISMATCH` (`DrawingManager.ReportGestureOutcome`, `GameManager`).
- **Đồ họa Retro Pixel-Art:** UI Phase 9 (font PressStart2P, 9-slice), Particle VFX, nét vẽ mượt bằng Catmull-Rom Spline/SMA như mực neon.
- **2 người chơi (Versus):** theo dõi độc lập Tay 1 và Tay 2. Điểm được tính cho người chơi ở nửa màn hình nơi nét vẽ bắt đầu (`VersusMatch.PlayerForScreenX`).

---

## 📂 Cấu Trúc Hệ Thống Cốt Lõi

- **`mediapipe_streamer.py`** (`Tools/`): đọc webcam, chạy MediaPipe Hand Landmarker, gửi tọa độ bàn tay (UDP 9050) và ảnh camera JPEG (UDP 9051) cho Unity.
- **`HandTrackingBridge.cs`:** **đường khởi chạy Python duy nhất** (tối đa 1 lần mỗi phiên Play, chỉ ở chế độ AR), nhận UDP bằng luồng nền, lọc nhiễu tọa độ (One Euro) và xử lý logic chụm tay (Hysteresis). Có cả chế độ giả lập bằng chuột.
- **`ProjectEnv.cs`:** đọc file `.env` (nếu có) và chọn Python: `PYTHON_EXE` trong `.env` → `.venv` của dự án → báo lỗi tiếng Việt kèm hướng dẫn. Không bao giờ dùng Python hệ thống.
- **`InputModeSettings.cs`:** chế độ nhập (AR / Mouse Simulation) lưu cục bộ trên máy (EditorPrefs hoặc `INPUT_MODE` trong `.env`), nên đổi chế độ **không làm thay đổi scene**.
- **`MediaPipeUDPReceiver.cs`:** nhận luồng video (UDP 9051) để vẽ nền camera. Không mở thêm luồng nhận landmark nếu `HandTrackingBridge` đã nhận; muốn khởi chạy Python thì giao lại cho `HandTrackingBridge`.
- **`DrawingManager.cs`:** quản lý thao tác vẽ (LineRenderer), chờ khi mất tracking, nét nhiều nét và gửi điểm vẽ đi nhận diện.
- **`SentisRecognizer.cs` / `PRecognizer.cs`:** nhận diện ký hiệu (Sentis ONNX là mặc định, `$P` là dự phòng).
- **`GestureCompletionPolicy.cs`:** cổng hình học quyết định khi nào được chấm sớm (X, E, O và các nét đơn), cũng dùng để từ chối kết quả thiếu cấu trúc.
- **`CNNTestScene.cs` & `RecognitionSampleWriter.cs`:** scene thử riêng `Assets/Scenes/CNNRecognitionTest.unity` (không camera, không gameplay; cũng mở được qua `Window > Magic Touch > MobileMagicCNN Test`). Vẽ bằng chuột, bấm NHẬN DIỆN để xem xác suất từng lớp; chọn nhãn đúng rồi bấm **LƯU SAMPLE** để lưu nét vẽ (JSON + PNG) vào `Related/Data-Samples/` (bị `.gitignore`). Mẫu lưu không tự động được dùng để huấn luyện.
- **`GameManager.cs` & `UIManager.cs` & `MenuManager.cs`:** Trái tim của game. Quản lý vòng lặp trạng thái (Main Menu → Waiting for Camera → Playing → Paused/Game Over), hiển thị máu (Hearts), Combo và điểm số, chế độ Versus.
- **`EnemySpawner.cs` & `EnemyBalloon.cs`:** sinh kẻ địch (scene đang gán 3 sprite `Bat`, `Knight`, `Robot` trong `Assets/Sprites/Enemies/`) treo trên bóng bay mang ký hiệu ngẫu nhiên.

### Cấu trúc thư mục

```text
MagicTouch/
├── Assets/            # Game Unity: Scenes/SampleScene.unity, Scripts/, Prefabs/, Models/ (ONNX),
│                      #   Sprites/, Audio/, UI/ (Phase 9 + MenuManager), Editor/ (công cụ & kiểm tra)
├── Tools/             # Core Python: mediapipe_streamer.py, *.task, *.bat, requirements.txt (đã ghim),
│                      #   setup_python_env.bat/.sh. Tên và vị trí thư mục này bị GÁN CỨNG — không đổi tên/dời.
├── Related/           # Tài liệu không cần khi chạy game: Docs/, Logic-Notes/
│                      #   (Data-Samples/ và _ToDelete/ chỉ có trên máy, bị .gitignore)
├── Train/             # train_magic_touch.py: huấn luyện CNN và xuất magic_touch_model.onnx;
│                      #   finetune_caret.py, stroke_dataset.py, validate_caret.py, restore_caret_model.ps1,
│                      #   CARET_IMPROVEMENT.md. Thư viện huấn luyện riêng: Train/requirements-training.txt
├── hand_landmarker.task   # Model MediaPipe gốc (streamer tìm ở thư mục gốc trước, rồi tới Tools/)
├── .python-version    # 3.11
├── .env.example       # Mẫu cấu hình cục bộ; chép thành .env (bị .gitignore) nếu cần
├── .venv/             # Tạo bởi Tools\setup_python_env.bat (bị .gitignore)
├── Packages/, ProjectSettings/   # Cấu hình Unity
└── README.md
```

### Sơ đồ pipeline (chế độ AR)

```text
 Webcam
   │  OpenCV: CAP_DSHOW trước, lỗi thì CAP_MSMF (Windows), camera 0, 640x480
   ▼
 Tools/mediapipe_streamer.py ── MediaPipe HandLandmarker (hand_landmarker.task, đọc qua buffer)
   │  (Unity khởi chạy bằng .venv\Scripts\pythonw.exe hoặc PYTHON_EXE trong .env)
   │  JSON landmark ──► UDP 127.0.0.1:9050      JPEG (--stream-video) ──► UDP 127.0.0.1:9051
   ▼                                              ▼
 HandTrackingBridge (luồng nền nhận 9050)      MediaPipeUDPReceiver (luồng nền nhận 9051)
   │  lọc session-id (TrackingStreamSession)        └─► RawImage "CameraBackground" (nền camera)
   │  lọc One Euro / palm-relative, chống spike
   │  PinchTransitionFilter (vào 0.04 / ra 0.08)
   ▼
 DrawingManager ── vẽ LineRenderer, chờ mất tracking 0.14 s, chờ nét kế 0.5 s
   ▼
 RecognizePoints: SentisRecognizer (ONNX, mặc định) │ dự phòng: PRecognizer ($P)
   ▼  OnGestureRecognized
 GameManager.HandleGestureRecognized ─► OnSymbolRecognized(symbol)
   ▼
 EnemyBalloon.Pop()  (nổ mọi bóng trùng ký hiệu; điểm combo 10/30/60…)
```

Chế độ Mouse Simulation bỏ qua toàn bộ phần Python/UDP: không khởi chạy Python, `HandTrackingBridge` và `DrawingManager` đọc chuột trực tiếp, nền camera dùng `WebCamTexture` của Unity. Ở AR thì Python giữ webcam, Unity chỉ hiển thị video Python gửi sang.

---

## 🛠️ Yêu Cầu Hệ Thống

- **Unity Editor 6000.5.6f1** (Unity 6, đọc từ `ProjectSettings/ProjectVersion.txt`).
- **Windows** (macOS chưa hỗ trợ chính thức).
- **Webcam** (chỉ cần cho chế độ AR).
- **Python 3.11** (chỉ cần cho chế độ AR) — cài từ [python.org](https://www.python.org/downloads/), nhớ chọn **"py launcher"** khi cài. Thư viện được ghim trong `Tools/requirements.txt`: `mediapipe==1.1.1`, `opencv-contrib-python==5.0.0.93`, `numpy==2.4.6`.

---

## 🎮 Cách Chạy Từ Máy Mới

1. **Cài Unity 6000.5.6f1** qua Unity Hub, và **Python 3.11** từ python.org (chọn "py launcher"). Python chỉ cần cho chế độ AR.
2. **Clone dự án** (nên đặt ở đường dẫn ngắn, ví dụ `D:\Projects\MagicTouchAR`), rồi chuyển sang nhánh có portable setup:
   ```bash
   git clone https://github.com/Daitran18031005/MagicTouchAR.git
   cd MagicTouchAR
   git checkout QuangDai_Feature
   ```
   Khi PR vào `main` đã được gộp, chỉ cần clone mặc định (bỏ bước `git checkout`).
3. **Tạo môi trường Python cho dự án:** chạy `Tools\setup_python_env.bat` (double-click, hoặc chạy trong terminal ở thư mục gốc dự án):
   ```bat
   Tools\setup_python_env.bat
   ```
   - Script tìm Python 3.11 (`py -3.11`, hoặc `python` trên PATH nếu đúng là 3.11), tạo `.venv` trong thư mục dự án, cài các gói đã ghim, rồi in `OK: cv2 5.0.0 | mediapipe 1.1.1 | numpy 2.4.6` và `Done: .venv is ready (Python 3.11).`
   - Nếu dùng Python 3.11 khác (ví dụ một conda env), truyền đường dẫn: `Tools\setup_python_env.bat C:\đường\dẫn\python.exe`.
   - Nếu `.venv` đã tồn tại nhưng không phải 3.11, script dừng và nhắc xóa thư mục `.venv` rồi chạy lại.
4. **Mở dự án bằng Unity Hub** (Unity 6000.5.6f1) và mở scene `Assets/Scenes/SampleScene.unity`.
5. **Chọn chế độ nhập:** `Tools > Magic Touch > Switch Input > AR Hand Tracking (MediaPipe)` hoặc `> Mouse Simulation (PC Debug)`. Mục đang chọn có dấu tick.
   - Lựa chọn lưu **trên máy của bạn** (EditorPrefs), **không làm thay đổi scene**. Mặc định (chưa chọn gì) là AR.
   - `Use Scene or .env Default` xóa lựa chọn đã lưu.
6. **Bấm Play**, rồi chọn **PLAY** ở menu chính.
   - **AR:** Unity tự khởi chạy streamer **một lần** bằng `.venv`. Console có dòng `[HandTrackingBridge] Launched Python Streamer … with …\.venv\Scripts\pythonw.exe`. Đưa tay vào khung hình; màn *"Waiting for Camera Setup..."* tự tắt khi có landmark đầu tiên. Chụm ngón cái + ngón trỏ để vẽ.
   - **Mouse Simulation:** giữ chuột trái và kéo để vẽ (nửa trái màn hình là Tay 1, nửa phải là Tay 2). Không có Python.

### Lưu ý khi chạy

- **Phải cài đúng Unity 6000.5.6f1.** Nếu Unity Hub không liệt kê bản này ở mục *Install Editor*, tìm trong tab **Archive** (trang tải bản cũ của Unity).
- **Lần mở Unity đầu tiên** sẽ nhập tài nguyên (tạo thư mục `Library/`), mất vài phút. Các lần sau nhanh hơn.
- **Lỗi đỏ có thể bỏ qua:** các dòng về `MCP Unity` / `Unity-MCP` (trùng cổng 8080/8090) và các dòng `NoSubscription` của Unity AI là nhiễu đã biết, **không ảnh hưởng game**.
- **Repo private:** nếu repo đang để private, bạn cần được chủ repo mời làm *collaborator* thì mới clone được.

### Cấu hình tùy chọn (`.env`)

Chép `.env.example` thành `.env` ở thư mục gốc dự án (file này bị `.gitignore`):

| Khóa | Ý nghĩa |
|---|---|
| `PYTHON_EXE` | Đường dẫn Python dùng cho tracker. Để trống thì dùng `.venv` |
| `LANDMARK_UDP_PORT`, `VIDEO_UDP_PORT` | Cổng UDP (mặc định 9050 / 9051), áp dụng cho cả Unity và streamer |
| `CAMERA_INDEX` | Webcam dùng (`--camera`), 0 = webcam đầu tiên |
| `INPUT_MODE` | `ar` hoặc `mouse` khi chưa chọn qua menu Switch Input. Bản build luôn dùng AR |

### Chạy tracker bằng tay (debug)

Unity đã tự khởi chạy tracker ở chế độ AR. Nếu muốn chạy tay để xem cửa sổ camera:
1. Trên GameObject `HandTrackingBridge`, bỏ chọn *Auto Launch Python Streamer* và *Auto Launch Python On Play*. Lý do: khi tự khởi chạy, Unity bỏ qua gói UDP không mang session-id của nó (`TrackingStreamSession.cs`).
2. Trong terminal ở thư mục gốc dự án:
   ```bat
   .venv\Scripts\activate
   Tools\run_hand_tracker_with_preview.bat
   ```
   Cửa sổ **"MediaPipe AR Hand Tracker"** hiện hình camera. Terminal in `[STREAMER] Camera: … FPS | Tracking: … FPS | … | Hands: N` mỗi 2 giây.

### Cách chơi

- **Vị trí:** Đứng trước Webcam (cách khoảng 0.5m - 1m), đảm bảo phòng có đủ ánh sáng.
- **Bắt đầu vẽ:** Đưa tay lên, chụm ngón trỏ và ngón cái lại với nhau. Nét vẽ ma thuật sẽ xuất hiện trên màn hình bám theo ngón tay của bạn.
- **Kết thúc nét vẽ:** Mở ngón tay ra.
- **Nối nét (các chữ như X, E):** sau khi mở tay để vẽ xong nét 1, bạn có khoảng **0,5 giây** (giá trị `multiStrokeDelay` trong scene) để chụm tay lại và vẽ tiếp nét 2. Quá thời gian đó không vẽ thêm, game sẽ đem hình đi nhận diện.
- **Mục tiêu:** Hãy nhìn ký tự trên quả bóng bay của kẻ địch đang rơi. Vẽ ký hiệu tương tự lên không trung để làm nổ bóng. Ở chế độ Solo, nếu kẻ địch chạm đất, bạn sẽ mất 1 Trái tim (Mạng).
- **Tạm dừng:** phím **Esc**.

---

## 🩹 Lỗi Thường Gặp (đã gặp thật)

| Hiện tượng | Nguyên nhân | Cách xử lý |
|---|---|---|
| `setup_python_env.bat` báo `Python 3.11 was not found` | Máy chưa có Python 3.11 (ví dụ chỉ có 3.14) | Cài Python 3.11 từ python.org (chọn "py launcher"), hoặc truyền đường dẫn một Python 3.11 cho script |
| Console AR báo `Không tìm thấy Python cho hand tracker…` hoặc `Python streamer đã thoát (mã …)` | Chưa có `.venv`, hoặc `.venv` hỏng/thiếu gói | Chạy `Tools\setup_python_env.bat`. Nếu nó báo `.venv` không phải 3.11 thì xóa thư mục `.venv` rồi chạy lại |
| `pip` báo `OSError: [WinError 3] The system cannot find the path specified` (gợi ý "Long Path") | Đường dẫn thư mục dự án quá dài (giới hạn 260 ký tự của Windows) | Đặt dự án ở đường dẫn ngắn hơn hoặc bật Windows Long Path support |
| Mở webcam treo 38–43 giây | Backend `CAP_MSMF` dò thiết bị rất chậm trên một số webcam Windows | Đã xử lý: streamer thử `CAP_DSHOW` trước (`mediapipe_streamer.py:61-64`). Đánh đổi: DSHOW đo được ~15 FPS, MSMF ~30 FPS |
| MediaPipe crash (`abort()`) khi đường dẫn project có dấu tiếng Việt | Đường dẫn Unicode truyền vào tầng C++ của MediaPipe | Đã xử lý: đọc model bằng Python rồi truyền qua `model_asset_buffer` (`mediapipe_streamer.py:193-195`) |

---

## 🗺️ Kế Hoạch Tiếp Theo (CHƯA LÀM)

- **Bản Android:** chơi bằng cảm ứng (Python tracker không chạy trên mobile).
- **Animation và hệ thống 3 map** (lâu đài, tháp đồng hồ, Halloween).
- **Kiểm thử trên macOS** (`Tools/setup_python_env.sh` chưa được thử).

Chi tiết dọn dẹp project và các việc còn tồn: [Related/Docs/CLEANUP_REPORT.md](Related/Docs/CLEANUP_REPORT.md).

---

## 🧑‍💻 Đóng Góp (Contributing)

Đây là một dự án cá nhân/học thuật. Mọi ý kiến đóng góp, báo lỗi (Issues) hoặc Pull Requests đều được chào đón!

**Bản quyền đồ họa:** Các tài nguyên đồ họa Pixel Art trong dự án được tạo bằng AI (DALL-E/Midjourney) và đã được tự động cắt (Auto-Slice) thông qua công cụ Editor script nội bộ. Font PressStart2P đi kèm giấy phép OFL (`Assets/UI/Phase9/OFL.txt`).
