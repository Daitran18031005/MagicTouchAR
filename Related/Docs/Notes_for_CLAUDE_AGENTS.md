# Notes for CLAUDE.md / AGENTS.md (draft input)

Các ý dưới đây được lấy từ tài liệu cũ và đã **đối chiếu với code ngày 2026-10-16** (branch `cleanup/organize`).
Mỗi ý ghi rõ nguồn và vị trí code đã kiểm. Đây chưa phải CLAUDE.md/AGENTS.md.

## 1. MediaPipe model được đọc bằng buffer, không dùng đường dẫn
- Nếu đường dẫn dự án có ký tự Unicode (ví dụ thư mục tiếng Việt có dấu), MediaPipe ở tầng C++ sẽ `abort()` khi nhận `model_asset_path`.
- Vì vậy streamer đọc file `.task` bằng Python rồi truyền vào qua `BaseOptions(model_asset_buffer=...)`. Không được đổi lại thành `model_asset_path`.
- Code: `Tools/mediapipe_streamer.py:193-195` (`_create_base_options`).
- Nguồn: `Related/Docs/anti-4-10.readme`, mục "Lỗi 1".

## 2. Camera trên Windows: ưu tiên DSHOW, MSMF là phương án dự phòng
- Khi mở bằng `cv2.CAP_MSMF`, có webcam mất khoảng 38 giây để dò thiết bị, khiến game đứng ở "Waiting for Camera Setup...". Vì vậy streamer thử `CAP_DSHOW` trước, nếu không mở được mới dùng `CAP_MSMF`.
- Đánh đổi cần biết: theo số đo thực tế, DSHOW 640x480 báo 60 FPS nhưng chỉ đạt **~15 FPS**, còn MSMF đạt **~30 FPS** (nguồn: `fps-validation.md`).
- Code: `Tools/mediapipe_streamer.py:59-65`.
- Nguồn: `Related/Docs/anti-4-10.readme`, mục "Lỗi 2"; `Related/Docs/fps-validation.md`.

## 3. Chơi thử bằng chuột (không cần camera)
- Trong Unity: `Tools > Magic Touch > Switch Input > Mouse Simulation (PC Debug)`, sau đó Play, giữ chuột trái để vẽ.
- Lệnh này bật `_enableEditorSimulation` trên `DrawingManager` và `HandTrackingBridge`. Muốn quay lại camera thật thì chọn `Switch Input > AR Hand Tracking (MediaPipe)`.
- Lưu ý: lệnh này đánh dấu scene là đã sửa (dirty). Đừng lưu scene khi đang ở chế độ chuột nếu không chủ ý.
- Code: `Assets/Editor/MagicTouchSceneSetup.cs:443-470`.
- Nguồn: `Related/Docs/anti-4-10.readme`, mục "Hướng dẫn chạy game nhanh – Cách 2".

## 4. FPS đo thật và thời gian chờ khi mất tracking
- Số đo thật (không phải 60 FPS như README cũ ghi):
  - MSMF: ~30 FPS camera.
  - Play Mode với 1 tay: camera 30,24 FPS, tracking 28,75 FPS, inference 29,4 ms.
  - Các lần chạy chẩn đoán không có tay: ~20–23 FPS tracking.
- Chỉ số trực tiếp: Inspector `HandTrackingBridge > Measured Camera Performance`. File `Logs/tracking-performance.json` được streamer ghi liên tục.
- **Thời gian chờ khi mất tracking: 140 ms**. Đã khớp với code: `DrawingManager.trackingLossGrace = 0.14f` (`Assets/Scripts/Drawing/DrawingManager.cs:37`, scene cũng là 0.14).
- **Không còn mốc "80 ms" khi nhả tay.** Code hiện tại xác nhận việc nhả tay theo *mẫu*, không theo thời gian: một mẫu mở tay chưa rõ ràng chỉ tạm dừng nét vẽ, mẫu kế tiếp mới xác nhận là nhả tay hay vẫn đang chụm (`Assets/Scripts/Tracking/PinchTransitionFilter.cs`). Ngưỡng chụm: `pinchEnterThreshold 0.04` / `pinchExitThreshold 0.08` (`HandTrackingBridge.cs:39,42`).
- Nguồn: `Related/Docs/fps-validation.md`.

## 5. Quy ước đặt tên
- Field private/serialized dùng `_camelCase` (ví dụ `_worldCamera`, `_enableEditorSimulation`). Class, method và property dùng `PascalCase`. Code hiện tại tuân theo quy ước này.
- Khác với quy tắc cũ: nhiều field **public** đang dùng `camelCase` (ví dụ `pinchEnterThreshold`, `multiStrokeDelay`) chứ không phải `PascalCase`. Không đổi tên chúng, vì đổi tên sẽ làm mất giá trị đã lưu trong scene trừ khi thêm `FormerlySerializedAs`.
- Nguồn: `Related/_ToDelete/agent_rules.md`, mục 2 (chỉ giữ phần còn khớp với code).

## Đã loại bỏ (lỗi thời)
- "Đạt chuẩn 60 FPS" và môi trường Python 3.13 (`anti-4-10.readme`). Hiện tại `.venv` dùng Python 3.11; Python 3.14 của hệ thống thiếu numpy.
- Tech stack cũ trong `project_brief.md` / `agent_rules.md`: $1 Unistroke, MediaPipe Unity Plugin, chụm ngón cái + ngón giữa, Object Pooling, `BRIEF.md`. Code hiện tại dùng Sentis CNN, nhận dữ liệu từ Python qua UDP, chụm ngón cái + ngón trỏ, và `Instantiate`/`Destroy`.
