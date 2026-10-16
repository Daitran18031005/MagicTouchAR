# AGENTS.md — Luật làm việc cho mọi agent (Claude Code, Codex, Antigravity…)

## 1. Tổng quan
- **AR Magic Touch:** game vẽ ký hiệu (V, ^, —, |, O, Z, W, S, E, X) để nổ bóng bay; chế độ Solo và Versus.
- **Unity 6000.5.6f1** (xem `ProjectSettings/ProjectVersion.txt`), scene chính `Assets/Scenes/SampleScene.unity`.
- **Python chỉ dùng cho tracker AR chạy trên PC** (`Tools/mediapipe_streamer.py`), gửi dữ liệu tay sang Unity qua UDP.
- Python của dự án là **3.11** trong `.venv` (tạo bằng `Tools\setup_python_env.bat`, gói ghim ở `Tools/requirements.txt`), hoặc `PYTHON_EXE` trong `.env`.
- Nhận diện ký hiệu chạy ngay trong Unity (model ONNX `Assets/Models/magic_touch_model.onnx`).
- Nhánh tích hợp: `QuangDai_Feature` (đã có portable setup). `main` chưa có các thay đổi này cho tới khi PR vào `main` được gộp.

## 2. LUẬT CỨNG — không bao giờ làm
- Không chạy menu `Tools > Magic Touch > *` khi chưa hỏi (phần lớn tự lưu scene và ghi đè prefab; `Switch Input` đổi cấu hình của máy người dùng).
- Không tự chạy các menu khác dưới `Tools/` của dự án, trừ 3 menu `Validate` ở mục 5 (`Build Reference UI`/`Preview` sửa object trong scene; `Auto Slice` sửa cài đặt import sprite).
- Không đổi tên/dời các đường dẫn gán cứng ở mục 6 (code và script `.bat` tham chiếu bằng chuỗi).
- Không đổi tên field public/serialized hiện có (mất giá trị đã lưu trong scene/prefab).
- Không xóa hoặc dời file `.meta` riêng lẻ; dời asset trong `Assets/` phải kèm `.meta`, hoặc dùng `AssetDatabase.MoveAsset` (giữ GUID).
- Không sửa tay file `.unity`/`.prefab` (YAML) trừ khi được yêu cầu rõ.
- Không `Instantiate`/`Destroy` trong `Update` (tạo/hủy trong coroutine hoặc event).
- Không commit `.env`, `.venv/`, `Library/`, `Logs/`, `Related/_ToDelete/`.
- Không push `main`, không force push, không merge vào `main` khi chưa hỏi.
- Không cho Unity dùng Python hệ thống (`pythonw`/`python` trên PATH). Chỉ khởi chạy Python qua `HandTrackingBridge.StartPythonStreamerStealth` (đường duy nhất, tối đa 1 lần mỗi phiên Play, chỉ ở AR).

> ⚠️ **CẢNH BÁO: `MagicTouchSceneSetup` KHÔNG còn tự chạy khi domain reload kể từ commit `e76334b`.**
> Phần nối dây cũ giờ là menu thủ công `Tools > Magic Touch > Repair Scene Wiring (manual)`, chỉ thêm thứ còn thiếu.
> Các menu `Tools > Magic Touch > *` **vẫn lưu `SampleScene.unity`** khi được bấm. Riêng `Switch Input` (từ commit `c9592de`) chỉ ghi EditorPrefs của máy, **không sửa/lưu scene**.
> - Sau khi chạy bất kỳ menu nào ở trên, hoặc sau khi mở Unity/compile, **LUÔN xem `git diff -- Assets/Scenes/SampleScene.unity`**.
> - Không commit thay đổi scene mà bạn không chủ ý.
> - Muốn hoàn tác (`git restore`) thì **hỏi trước**: scene trên máy có thể đang chứa thay đổi chủ dự án muốn giữ.

## 3. HỎI TRƯỚC KHI LÀM
- Đổi tên, dời hoặc xóa bất kỳ file/thư mục nào.
- Sửa scene hoặc prefab (kể cả gián tiếp qua menu Editor).
- Thêm hoặc nâng cấp package (Unity `Packages/manifest.json`, Python `Tools/requirements.txt`).
- Đổi phiên bản Python hoặc Unity.
- Sửa code khởi chạy Python (`HandTrackingBridge`, `MediaPipeUDPReceiver`, các file `.bat` trong `Tools/`).

## 4. CÁCH LÀM VIỆC
- Bổ sung, không viết lại class hiện có; chỉ làm đúng việc được giao.
- Code mới: hiệu ứng đặt trong `Assets/Scripts/Juice/`, map/theme đặt trong `Assets/Scripts/Themes/` (hai thư mục này **chưa tồn tại**, tạo khi cần).
- Kẻ địch: chỉ animate object con `EnemyBody`, không animate root (root do `EnemyBalloon` điều khiển rơi).
- UI và hiệu ứng dùng `Time.unscaledDeltaTime` (Pause đặt `Time.timeScale = 0`).
- Trước khi nói "xong": compile sạch (không lỗi mới trong Console); báo danh sách file đã sửa và cách chủ dự án tự kiểm tra.

## 5. KIỂM TRA
- 3 bộ check trong Editor (an toàn, không lưu scene):
  - `Tools/MagicTouch/UI/Validate UI Bindings`
  - `Tools/MagicTouch/Validate Interaction Repair`
  - `Tools/MagicTouch/Validate Drawing Repair`
- Môi trường Python: `Tools\setup_python_env.bat` phải in `OK: cv2 … | mediapipe … | numpy …` (dùng lại `.venv` nếu đã đúng 3.11).
- Unit test Python, chạy từ thư mục `Tools/`: `..\.venv\Scripts\python -m unittest test_stream_protocol test_hand_identity`.
- Lỗi Console cũ đã biết, **không phải do agent gây ra**:
  - Trùng cổng 8080 (`UnityMCPBridge`) và 8090 (`mcp-unity`).
  - Font `LivesText` thiếu ký tự ❤.
  - "Destroy may not be called from edit mode" khi chạy `Validate Interaction Repair`.

## 6. CẤU TRÚC VÀ ĐƯỜNG DẪN GÁN CỨNG
```text
Assets/    game Unity: Scenes/, Scripts/, Prefabs/, Models/, Sprites/, Audio/, UI/, Editor/ (công cụ + check)
Tools/     core Python: streamer, model *.task, *.bat, requirements.txt (ghim), setup_python_env.bat/.sh, unit test
Related/   tài liệu không cần khi chạy game (Docs/, Design/, Reference-Art/, Logic-Notes/)
Train/     train_magic_touch.py: huấn luyện và xuất model ONNX
Logs/      log và kết quả đo/kiểm tra (bị .gitignore, nhưng code ghi vào đây)
.venv/     Python 3.11 của dự án (bị .gitignore); .env (tùy chọn, bị .gitignore), mẫu ở .env.example
```

| Đường dẫn (KHÔNG đổi tên/dời) | Đang được dùng bởi |
|---|---|
| `Tools/mediapipe_streamer.py` | `HandTrackingBridge.cs`, `run_hand_tracker.bat`, `run_hand_tracker_with_preview.bat` |
| `Tools/hand_landmarker_fast.task` | `HandTrackingBridge.cs`, `prepare_fast_hand_model.py` |
| `Tools/run_hand_tracker.bat` | `MagicTouchSceneSetup.cs` |
| `Tools/hand-motion-trace.txt` | `HandMotionTrace.cs` |
| `Tools/drawing-editor-command.txt` | `DrawingRepairChecks.cs` |
| `hand_landmarker.task` (thư mục gốc) | `mediapipe_streamer.py`, `prepare_fast_hand_model.py` |
| `Tools/requirements.txt` | `setup_python_env.bat`, `setup_python_env.sh` |
| `.venv/` (thư mục gốc) | `ProjectEnv.cs`, `setup_python_env.bat`, `setup_python_env.sh` |
| `.env` (thư mục gốc, tùy chọn) | `ProjectEnv.cs`, `InputModeSettings.cs` |
| `Logs/` | `mediapipe_streamer.py`, `benchmark_camera.py`, `DrawingRepairChecks.cs`, `HandMotionTrace.cs` |

## 7. PIPELINE TÓM TẮT
```text
Webcam ─► Tools/mediapipe_streamer.py ─► UDP 9050 (landmark) ─► HandTrackingBridge (lọc tay, pinch)
                                     └─► UDP 9051 (video)    ─► MediaPipeUDPReceiver (nền camera)
HandTrackingBridge ─► DrawingManager (vẽ nét) ─► SentisRecognizer (mặc định) | PRecognizer (dự phòng)
                   ─► GameManager.OnSymbolRecognized ─► EnemyBalloon.Pop
```
- **Python:** `ProjectEnv` chọn `PYTHON_EXE` trong `.env` → `.venv\Scripts\pythonw.exe` → báo lỗi tiếng Việt; không rơi về Python hệ thống.
- **Chế độ nhập:** **AR** (mặc định trong scene) và **Mouse Simulation** (chỉ trong Editor, chuột thay tay, **không mở Python**).
- Chế độ là cấu hình cục bộ (`InputModeSettings`): EditorPrefs (menu `Switch Input`) → `INPUT_MODE` trong `.env` → giá trị scene; đổi chế độ không làm bẩn scene. Vẫn hỏi trước khi bấm menu (luật mục 2).
- AR: Python giữ webcam, Unity chỉ hiện video. Mouse Simulation: Unity tự mở webcam bằng `WebCamTexture`.

## 8. QUY ƯỚC CODE
- Field private/serialized: `_camelCase` (ví dụ `_worldCamera`, `_enableEditorSimulation`).
- Class, method, property: `PascalCase`.
- Field public hiện có dùng `camelCase` (ví dụ `pinchEnterThreshold`, `multiStrokeDelay`): giữ nguyên, không đổi tên.
- Namespace `MagicTouch.<Nhóm>` theo nhóm thư mục: `Tracking`, `Drawing`, `Recognition`, `Gameplay`, `UI`, `CameraControl` (thư mục `Scripts/Camera/`).

## 9. HIỂU LẦM HAY GẶP
- Python tracker chỉ chạy trên PC. Tablet/Android phải dùng cảm ứng hoặc tracking ngay trong Unity. `NativeMediaPipeBridge` chưa được dùng (không có trong scene); chưa có bản build Android.
- Chạy streamer bằng tay thì phải tắt *Auto Launch Python Streamer* (`HandTrackingBridge`) và *Auto Launch Python On Play* (`MediaPipeUDPReceiver`); nếu không, Unity bỏ qua dữ liệu vì lệch session-id (`TrackingStreamSession`).
- `.venv` phải là Python 3.11. `Tools/setup_python_env.bat` từ chối dùng lại `.venv` sai phiên bản; khi đó xóa thư mục `.venv` rồi chạy lại (hỏi trước khi xóa).
- `OneDollarRecognizer` và hàm snapping `TryGetMagneticSnapPoint` có trong code nhưng **không được dùng**.

## 10. VIỆC ĐANG MỞ — chỉ làm khi được giao
- Font `LiberationSans SDF` thiếu ký tự ❤ trong `LivesText` (HUD); chưa xử lý.
- `BalloonPopEffect` dùng `Resources.Load` cho sprite VFX khi build, trong khi dự án không có thư mục `Resources/`.
- macOS: `Tools/setup_python_env.sh` và đường `.venv/bin/python3` chưa được kiểm thử.

## 11. ĐỌC THÊM
- [README.md](README.md): giới thiệu, trạng thái, cách chạy từ máy mới (Python 3.11, `setup_python_env.bat`, chọn chế độ nhập), lưu ý khi chạy, lỗi thường gặp.
- [Related/Docs/Notes_for_CLAUDE_AGENTS.md](Related/Docs/Notes_for_CLAUDE_AGENTS.md): chi tiết có nguồn file:dòng, các thông tin đã lỗi thời.
- [Related/Docs/CLEANUP_REPORT.md](Related/Docs/CLEANUP_REPORT.md): lịch sử dọn dẹp, kết quả kiểm tra, việc tồn đọng.
