# Notes for CLAUDE.md / AGENTS.md (draft input)

Nguyên liệu để viết CLAUDE.md/AGENTS.md sau này. Mọi ý đã được **đối chiếu với code ngày 2026-10-16** (nhánh `feature/portable-setup`, sau Pha 1–3 của portable setup) và ghi rõ nguồn (file:dòng hoặc file gốc). Ý nào chưa kiểm chứng được ghi "chưa kiểm tra".
[AGENTS.md](../../AGENTS.md) và [CLAUDE.md](../../CLAUDE.md) **đã được tạo ở gốc project** (bản luật ngắn, dựa trên ghi chú này); file này giữ phần chi tiết có nguồn.

## A. Luật làm việc

Nguồn: chỉ thị của chủ dự án (2026-10-16), trừ khi ghi khác.

1. **Không chạy menu `Tools > Magic Touch > *` khi chưa hỏi.** Các lệnh này tự lưu scene (`EditorSceneManager.SaveScene`): `Assets/Editor/MagicTouchSceneSetup.cs:39,132,151,457,686`. `MagicTouchPrefabBuilder.cs:87-90` ghi đè prefab. **Ngoại lệ (từ commit `c9592de`):** `Switch Input > *` chỉ ghi lựa chọn vào EditorPrefs của máy, **không sửa/lưu scene** (`MagicTouchSceneSetup.cs:499-541`); vẫn hỏi trước khi chạy.
2. **`MagicTouchSceneSetup` KHÔNG còn tự chạy khi domain reload** kể từ commit `e76334b` (đã gỡ `[InitializeOnLoad]`, static constructor và `delayCall`). Phần nối dây cũ là menu thủ công `Tools > Magic Touch > Repair Scene Wiring (manual)`: chỉ thêm thứ còn thiếu, không ghi đè giá trị đã có (kể cả chế độ nhập), và chỉ thêm listener `RestartGame` khi chưa có. Các menu `Tools > Magic Touch > *` vẫn lưu scene khi được bấm, nên sau khi chạy chúng (hoặc sau khi mở Unity/compile) vẫn phải xem `git diff` của scene trước khi commit. (Nguồn: `Related/Docs/CLEANUP_REPORT.md` mục 7.)
3. **Kẻ địch chỉ animate object con `EnemyBody`**, không animate root (root do `EnemyBalloon` điều khiển rơi). Code: field `enemyBodyTransform` (`Assets/Scripts/Gameplay/EnemyBalloon.cs:38`), tìm con tên `EnemyBody` (`EnemyBalloon.cs:95`); prefab `Assets/Prefabs/EnemyBalloon.prefab` có object `EnemyBody`.
4. **Code mới đặt trong `Assets/Scripts/Juice/` (hiệu ứng) và `Assets/Scripts/Themes/` (map/theme).** Hai thư mục này **chưa tồn tại** (kiểm tra 2026-10-16).
5. **Không `Instantiate`/`Destroy` trong `Update`.** Hiện code tạo object trong coroutine hoặc event, không trong `Update`: `EnemySpawner.cs:230`, `EnemyBalloon.cs:331`, `GameManager.cs:948`.
6. **UI và hiệu ứng dùng `Time.unscaledDeltaTime`**, vì Pause đặt `Time.timeScale = 0` (`Assets/UI/Scripts/MenuManager.cs`, `PauseGame`). Các timer của `DrawingManager` đã theo cách này (`DrawingManager.cs:319,405`).
7. **Compile sạch (Console không lỗi mới) trước khi báo xong.** Lỗi cũ đã biết, không tính: `UnityMCPBridge` trùng cổng 8080, `mcp-unity` trùng cổng 8090, thiếu ký tự ❤ trong font `LivesText`. (4 lỗi `Destroy may not be called from edit mode` khi chạy `Validate Interaction Repair` **đã sửa ở `0fe5b28`**: `DrawingManager.ClearAllLines` dùng `DestroyImmediate` ngoài Play Mode; nếu thấy lại là lỗi mới.)
8. Các bộ kiểm tra có sẵn trong Editor: `Tools/MagicTouch/UI/Validate UI Bindings`, `Tools/MagicTouch/Validate Interaction Repair`, `Tools/MagicTouch/Validate Drawing Repair` (`UIReferenceChecks.cs:12`, `InteractionRepairChecks.cs:17`, `DrawingRepairChecks.cs:108`). Ngày 2026-10-16 cả ba đều PASS.
9. **Quy ước đặt tên:** field private/serialized dùng `_camelCase` (ví dụ `_worldCamera`, `_enableEditorSimulation`); class, method, property dùng `PascalCase`. Nhiều field **public** đang là `camelCase` (`pinchEnterThreshold`, `multiStrokeDelay`). Không đổi tên chúng, vì sẽ mất giá trị đã lưu trong scene trừ khi thêm `FormerlySerializedAs`. (Nguồn: `Related/_ToDelete/agent_rules.md` mục 2, chỉ giữ phần khớp với code.)

## B. Đường dẫn bị gán cứng — KHÔNG đổi tên/dời

| Đường dẫn | Nơi dùng (đã xác minh) |
|---|---|
| `Tools/mediapipe_streamer.py` | `Assets/Scripts/Tracking/HandTrackingBridge.cs:315` (đường khởi chạy duy nhất), `Tools/run_hand_tracker.bat:9`, `Tools/run_hand_tracker_with_preview.bat:7` |
| `Tools/hand_landmarker_fast.task` | `HandTrackingBridge.cs:331` |
| `Tools/run_hand_tracker.bat` | `Assets/Editor/MagicTouchSceneSetup.cs:486` |
| `Tools/hand-motion-trace.txt` | `Assets/Editor/HandMotionTrace.cs:35` |
| `Tools/drawing-editor-command.txt` | `Assets/Editor/DrawingRepairChecks.cs:14` |
| `hand_landmarker.task` (thư mục gốc) | `Tools/mediapipe_streamer.py:183-187`: tìm `Tools/../hand_landmarker.task` trước, rồi `Tools/`, rồi thư mục hiện tại |
| `Tools/requirements.txt` | `Tools/setup_python_env.bat:42` |
| `.venv/` (thư mục gốc) | `Assets/Scripts/Tracking/ProjectEnv.cs:63` (`.venv\Scripts\pythonw.exe`/`python.exe`; macOS `.venv/bin/python3`), tạo bởi `Tools/setup_python_env.bat` |
| `.env` (thư mục gốc, tùy chọn) | `ProjectEnv.cs` (`Path.Combine(ProjectRoot, ".env")`); mẫu ở `.env.example` |
| `Logs/` (ghi kết quả) | `mediapipe_streamer.py:315` (`Logs/tracking-performance.json`), `DrawingRepairChecks.cs:15`, `HandMotionTrace.cs:83` |

## C. Kiến trúc / pipeline (chế độ AR)

1. **`Tools/mediapipe_streamer.py`:**
   - Mở webcam `--camera` (mặc định 0, 640x480). Trên Windows thử `cv2.CAP_DSHOW` trước, lỗi thì `CAP_MSMF` (dòng 59-65).
   - Chạy MediaPipe HandLandmarker, tối đa 2 tay. Bản mediapipe 1.x không còn `mp.solutions`, nên dùng adapter Tasks (dòng 159-254). Model đọc qua buffer.
   - Gửi JSON landmark tới UDP `127.0.0.1:9050` (dòng 27-28). Nếu có `--stream-video` thì gửi JPEG tới UDP `9051` (dòng 29), kèm tiền tố session.
2. **`HandTrackingBridge.cs`** (GameObject `HandTrackingBridge`, `DefaultExecutionOrder(-100)`):
   - `StartPythonStreamerStealth` (dòng 301-359) là **đường khởi chạy Python duy nhất**, gọi từ `Awake` khi `_autoLaunchPythonStreamer` bật (scene: bật). Hàm này:
     - Return im lặng ở Mouse Simulation (dòng 305).
     - Chỉ thử **1 lần mỗi phiên Play** (`_launchAttempted`, dòng 311), kể cả khi tiến trình trước đã chết.
     - Lấy Python từ `ProjectEnv.TryResolvePython` (dòng 322): `PYTHON_EXE` trong `.env` → `.venv` → lỗi tiếng Việt kèm hướng dẫn; **không dùng Python hệ thống**. Field `_pythonExecutable` trong scene được giữ nhưng không dùng.
     - Truyền `--port`/`--video-port`/`--camera` từ `.env` và `--session-id` mới (dòng 334).
     - `Process.Start` nằm trong `#if UNITY_EDITOR || UNITY_STANDALONE`. Nếu streamer thoát sớm, `ReportStreamerExit` (dòng 362) báo lỗi.
   - Luồng nền nhận UDP 9050 (dòng 434 trở đi). Gói sai session-id bị bỏ (`TrackingStreamSession.cs`).
   - Lọc One Euro / palm-relative và chặn spike đầu ngón (dòng 599-616). `PinchTransitionFilter` với ngưỡng vào 0.04 / ra 0.08.
   - Khi nhận gói đầu tiên thì gọi `GameManager.StartGame()` (handshake, dòng 272-278).
3. **`MediaPipeUDPReceiver.cs`** (cùng GameObject): nhận video 9051 rồi đưa vào RawImage `CameraBackground`. Không mở thêm luồng nhận landmark nếu bridge đã nhận (dòng 184-187). Đọc cổng từ `.env` (dòng 125-126). `StartPythonTracker` (dòng 496) **chỉ giao lại cho bridge**, không còn tự `Process.Start`; nên dù `_autoLaunchPythonOnPlay` bật (scene: bật), lần gọi từ `Start()` chỉ gặp chốt chặn của bridge và bị bỏ qua (có log).
4. **`DrawingManager.cs`** (GameObject `DrawingManager`):
   - Đọc trạng thái tay từ bridge mỗi frame và vẽ `LineRenderer`.
   - Chờ khi mất tracking 0.14 s; chờ nét tiếp theo `multiStrokeDelay` 0.35 s (giới hạn 0.20–0.45).
   - Nhận diện qua `RecognizePoints` (dòng 410-417).
   - **Mặc định là `SentisRecognizer`** (`RecognizerEngine.SentisNeuralNetwork`, scene `_engine: 0`). Nếu model không nạp được thì dùng `PRecognizer` ($P). `PRecognizer` cũng làm cổng hình học khi quyết định chấm sớm (dòng 419-429).
   - **`OneDollarRecognizer` không được dùng ở đâu** (chỉ có định nghĩa class). Hàm snapping `TryGetMagneticSnapPoint` có định nghĩa nhưng **không được gọi**.
5. **`DrawingManager.OnGestureRecognized`** (dòng 979) → **`GameManager.HandleGestureRecognized`** (dòng 622) → **`OnSymbolRecognized`** (dòng 659). Ở Versus thì đi qua `OnPlayerSymbolRecognized`, xác định người chơi theo điểm đầu nét. Cuối cùng gọi **`EnemyBalloon.Pop()`** (`GameManager.cs:730`, `EnemyBalloon.cs:307`) cho mọi bóng trùng ký hiệu.
6. **Chế độ nhập:**
   - Scene trong repo mặc định là **AR**: `_enableEditorSimulation: 0` trên bridge và DrawingManager, `CameraBackground._useMediaPipeStream: 1`.
   - **Chế độ nhập là cấu hình cục bộ** (`Assets/Scripts/Tracking/InputModeSettings.cs`), theo thứ tự: EditorPrefs `MagicTouch.InputMode` (menu Switch Input) → `INPUT_MODE` trong `.env` → giá trị scene. Bản build luôn dùng AR.
   - Lựa chọn được áp lúc chạy trong `Awake` của `HandTrackingBridge` (dòng 232-233), `DrawingManager` (dòng 217-218) và `ARCameraBackground` (dòng 50-51), nên **không làm bẩn scene**.
   - **AR:** Python giữ webcam, Unity chỉ hiện video stream. **Mouse Simulation:** không khởi chạy Python, nền dùng `WebCamTexture` của Unity (không tranh webcam).
   - Giả lập chuột chỉ có trong Editor (`#if UNITY_EDITOR`, `HandTrackingBridge.cs:254`).

## D. Bài học kỹ thuật còn đúng

1. **Đọc model MediaPipe bằng buffer, không dùng đường dẫn.** Đường dẫn Unicode (thư mục tiếng Việt có dấu) làm tầng C++ của MediaPipe `abort()`. Code: `Tools/mediapipe_streamer.py:193-195` (`model_asset_buffer`). Nguồn: `Related/Docs/anti-4-10.readme`, mục "Lỗi 1".
2. **Ưu tiên DSHOW, MSMF là phương án dự phòng.** MSMF có thể treo khoảng 38 s khi dò webcam. Đánh đổi đo được: DSHOW 640x480 chỉ ~15 FPS, MSMF ~30 FPS. Code: `mediapipe_streamer.py:59-65`. Nguồn: `anti-4-10.readme`, mục "Lỗi 2"; `Related/Docs/fps-validation.md`.
3. **Thời gian chờ khi mất tracking: 140 ms** (`DrawingManager.cs:37`, scene 0.14). **Không còn mốc "80 ms" khi nhả tay:** code hiện tại xác nhận việc nhả tay theo *mẫu* (`PinchTransitionFilter.cs`): một mẫu mở tay chưa rõ ràng chỉ tạm dừng nét, mẫu kế tiếp mới xác nhận. Nguồn: `fps-validation.md` (ghi 80/140 ms; chỉ 140 ms còn khớp code).
4. **FPS đo thật:**
   - MSMF ~30 FPS camera.
   - Play Mode với 1 tay: camera 30,24 / tracking 28,75 FPS / inference 29,4 ms (`fps-validation.md`).
   - Chạy thử ngày 2026-10-16 (DSHOW): camera ~10–15 FPS, tracking ~10 FPS.
   - Chỉ số trực tiếp: Inspector `HandTrackingBridge > Measured Camera Performance` và `Logs/tracking-performance.json`.
5. **Chạy thử bằng chuột:** `Tools > Magic Touch > Switch Input > Mouse Simulation (PC Debug)`, Play, giữ chuột trái để vẽ. Từ commit `c9592de`, lệnh này chỉ lưu lựa chọn vào EditorPrefs, không sửa scene; Python không được khởi chạy. Nguồn: `anti-4-10.readme`, "Cách 2"; `MagicTouchSceneSetup.cs:499-541`.

## E. Hiểu lầm cần tránh

1. **Python tracker CHỈ chạy trên PC.** Unity khởi chạy nó bằng `Process.Start`, chỉ trong `UNITY_EDITOR || UNITY_STANDALONE`. Trên tablet/Android phải chơi bằng cảm ứng, hoặc tracking ngay trong Unity. `Assets/Scripts/Tracking/NativeMediaPipeBridge.cs` chỉ là khung dự kiến cho mobile và **không có trong scene**. Chưa có bản build Android.
2. **Unity dùng Python nào? — ĐÃ GIẢI QUYẾT** (branch `feature/portable-setup`).
   - Python lấy từ `PYTHON_EXE` trong `.env`, nếu không có thì `.venv` của dự án; thiếu cả hai thì báo lỗi tiếng Việt. **Không bao giờ rơi về Python hệ thống** (trước đây Unity gọi `pythonw` trên PATH: Python 3.14 thiếu `numpy`).
   - `.venv` tạo bằng `Tools\setup_python_env.bat` với Python **3.11** (`.python-version`); gói ghim trong `Tools/requirements.txt`.
   - Đã kiểm end-to-end trên máy gốc: Python 3.11.7 (python.org) → `.venv` → Unity khởi chạy streamer đúng 1 lần, có landmark, vẽ được.
   - Đã thử thêm trên một máy thứ hai (clone mới): chạy được cả Mouse Simulation và AR.
   - Hai điểm khởi chạy cũ (`HandTrackingBridge.Awake()` và `MediaPipeUDPReceiver.Start()`) **đã gộp** về `StartPythonStreamerStealth`, có chốt chặn 1 lần mỗi phiên Play.
3. **Chạy streamer bằng tay khi auto-launch còn bật thì Unity bỏ qua dữ liệu.** `TrackingStreamSession.Begin()` (`HandTrackingBridge.cs:334`) đặt session-id khi Unity tự khởi chạy streamer. Gói không có session-id đó bị bỏ (`TrackingStreamSession.cs`, `Matches`). Phải tắt *Auto Launch Python Streamer* và *Auto Launch Python On Play* khi chạy tay.
4. **`.venv` phải đúng Python 3.11.** Bản `setup_python_env.bat` cũ tạo đè `.venv` bằng Python khác làm `pyvenv.cfg` và thư viện lệch phiên bản (`numpy._core._multiarray_umath` lỗi). Script mới từ chối dùng lại `.venv` không phải 3.11 và nhắc xóa thư mục `.venv` rồi chạy lại.
5. **"Hai panel Game Over chồng nhau" và listener `RestartGame` trùng không phải do người sửa tay**, mà do `MagicTouchSceneSetup` tự chạy (luật A2).

## F. Đã LỖI THỜI — không dùng

| Nội dung cũ | Thực tế hiện tại | Nguồn cũ |
|---|---|---|
| $1 Unistroke Recognizer | Sentis CNN (ONNX) mặc định, `$P` dự phòng. `OneDollarRecognizer.cs` còn trong repo nhưng không dùng | `agent_rules.md`, `project_brief.md` |
| MediaPipe Unity Plugin (homuler) | Python streamer gửi qua UDP; `NativeMediaPipeBridge` không dùng | `project_brief.md` |
| Chụm ngón cái + ngón **giữa** | Ngón cái (landmark 4) + ngón **trỏ** (landmark 8) | `project_brief.md` |
| Object Pooling | `Instantiate`/`Destroy` (ngoài `Update`) | `agent_rules.md` |
| `BRIEF.md` | Không tồn tại | `agent_rules.md` |
| "60 FPS" / "60+ FPS" | 10–30 FPS đo thật | `anti-4-10.readme`, README cũ |
| Python 3.13 | Ghim **3.11** (`.python-version`, `Tools/requirements.txt`); Unity dùng `.venv`/`PYTHON_EXE`, không dùng Python hệ thống | `anti-4-10.readme` |
| Unity gọi `pythonw` trên PATH; `setup_python_env.bat` dùng `python` bất kỳ | `ProjectEnv`: `.env` → `.venv` → lỗi; script setup tìm đúng Python 3.11 | code trước `3423ced` / `1125fcc` |
| `Switch Input` lưu chế độ nhập vào scene | Lưu vào EditorPrefs (hoặc `INPUT_MODE` trong `.env`), không đụng scene | code trước `c9592de` |
| Python khởi chạy 2 lần (Awake + Start) | Một đường khởi chạy, tối đa 1 lần mỗi phiên Play, chỉ ở AR | code trước `3423ced` |
| Unity 2022.3 | Unity 6000.5.6f1 | README cũ |
| Coyote Time 0.08 s; nối nét 0.5 s | Chờ mất tracking 0.14 s; chờ nét kế 0.35 s | README cũ |
| Snapping "hút nam châm" nét vẽ; ký tự "A" | Hàm snap không được gọi; 10 ký hiệu không có "A" | README cũ |
| Quái Knight, Gargoyle, Golem | Sprite trong scene: Bat, Knight, Robot | README cũ |
| 80 ms visible-release grace | Xác nhận nhả tay theo mẫu (`PinchTransitionFilter`) | `fps-validation.md` |

## G. Trạng thái cuối đợt portable setup

- 4 pha đã xong: Python 3.11 + gói ghim + `Tools\setup_python_env.bat` (Pha 1); `ProjectEnv` và một đường khởi chạy Python (Pha 2); chế độ nhập cục bộ qua `InputModeSettings`, Mouse không mở Python (Pha 3); kiểm chứng và tài liệu (Pha 4).
- Đã thử đạt trên máy gốc và một máy thứ hai (clone mới), cả Mouse Simulation lẫn AR.
- Nhánh tích hợp là `QuangDai_Feature` (gộp fast-forward từ `feature/portable-setup`). `main` chưa có các thay đổi này cho tới khi PR vào `main` được gộp. Người mới clone thì `git checkout QuangDai_Feature`.
- Lưu ý khi chạy: các dòng `MCP Unity` / `Unity-MCP` (trùng cổng 8080/8090) và `NoSubscription` là nhiễu đã biết. Lần mở Unity đầu tiên mất vài phút để nhập tài nguyên. Cần đúng Unity 6000.5.6f1 (tab Archive của Unity Hub nếu không thấy).
