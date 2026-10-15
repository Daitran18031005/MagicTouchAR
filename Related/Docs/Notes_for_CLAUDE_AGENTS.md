# Notes for CLAUDE.md / AGENTS.md (draft input)

Nguyên liệu để viết CLAUDE.md/AGENTS.md sau này. Mọi ý đã được **đối chiếu với code ngày 2026-10-16** (nhánh `QuangDai_Feature`, sau khi merge `cleanup/organize`) và ghi rõ nguồn (file:dòng hoặc file gốc). Ý nào chưa kiểm chứng được ghi "chưa kiểm tra".
[AGENTS.md](../../AGENTS.md) và [CLAUDE.md](../../CLAUDE.md) **đã được tạo ở gốc project** (bản luật ngắn, dựa trên ghi chú này); file này giữ phần chi tiết có nguồn.

## A. Luật làm việc

Nguồn: chỉ thị của chủ dự án (2026-10-16), trừ khi ghi khác.

1. **Không chạy menu `Tools > Magic Touch > *` khi chưa hỏi.** Các lệnh này tự lưu scene (`EditorSceneManager.SaveScene`): `Assets/Editor/MagicTouchSceneSetup.cs:43,137,156,401,487,632`. `MagicTouchPrefabBuilder.cs:87-90` ghi đè prefab. Kể cả `Switch Input > Mouse Simulation / AR Hand Tracking` cũng lưu scene (`MagicTouchSceneSetup.cs:457-488`).
2. **Lưu ý: `MagicTouchSceneSetup` tự chạy mỗi lần domain reload** (`[InitializeOnLoad]` + `delayCall`, `MagicTouchSceneSetup.cs:23-45`). Mỗi lần nó thêm 1 listener `RestartGame` và tự lưu `SampleScene.unity`. Sau khi mở Unity hoặc compile, luôn xem `git diff` của scene trước khi commit. (Nguồn: `Related/Docs/CLEANUP_REPORT.md` mục 7.)
3. **Kẻ địch chỉ animate object con `EnemyBody`**, không animate root (root do `EnemyBalloon` điều khiển rơi). Code: field `enemyBodyTransform` (`Assets/Scripts/Gameplay/EnemyBalloon.cs:38`), tìm con tên `EnemyBody` (`EnemyBalloon.cs:95`); prefab `Assets/Prefabs/EnemyBalloon.prefab` có object `EnemyBody`.
4. **Code mới đặt trong `Assets/Scripts/Juice/` (hiệu ứng) và `Assets/Scripts/Themes/` (map/theme).** Hai thư mục này **chưa tồn tại** (kiểm tra 2026-10-16).
5. **Không `Instantiate`/`Destroy` trong `Update`.** Hiện code tạo object trong coroutine hoặc event, không trong `Update`: `EnemySpawner.cs:230`, `EnemyBalloon.cs:331`, `GameManager.cs:948`.
6. **UI và hiệu ứng dùng `Time.unscaledDeltaTime`**, vì Pause đặt `Time.timeScale = 0` (`Assets/UI/Scripts/MenuManager.cs`, `PauseGame`). Các timer của `DrawingManager` đã theo cách này (`DrawingManager.cs:315,401`).
7. **Compile sạch (Console không lỗi mới) trước khi báo xong.** Lỗi cũ đã biết, không tính: `UnityMCPBridge` trùng cổng 8080, `mcp-unity` trùng cổng 8090, thiếu ký tự ❤ trong font `LivesText`, và 4 lỗi `Destroy may not be called from edit mode` khi chạy `Validate Interaction Repair`.
8. Các bộ kiểm tra có sẵn trong Editor: `Tools/MagicTouch/UI/Validate UI Bindings`, `Tools/MagicTouch/Validate Interaction Repair`, `Tools/MagicTouch/Validate Drawing Repair` (`UIReferenceChecks.cs:12`, `InteractionRepairChecks.cs:17`, `DrawingRepairChecks.cs:108`). Ngày 2026-10-16 cả ba đều PASS.
9. **Quy ước đặt tên:** field private/serialized dùng `_camelCase` (ví dụ `_worldCamera`, `_enableEditorSimulation`); class, method, property dùng `PascalCase`. Nhiều field **public** đang là `camelCase` (`pinchEnterThreshold`, `multiStrokeDelay`). Không đổi tên chúng, vì sẽ mất giá trị đã lưu trong scene trừ khi thêm `FormerlySerializedAs`. (Nguồn: `Related/_ToDelete/agent_rules.md` mục 2, chỉ giữ phần khớp với code.)

## B. Đường dẫn bị gán cứng — KHÔNG đổi tên/dời

| Đường dẫn | Nơi dùng (đã xác minh) |
|---|---|
| `Tools/mediapipe_streamer.py` | `Assets/Scripts/Tracking/HandTrackingBridge.cs:279`, `Assets/Scripts/Tracking/MediaPipeUDPReceiver.cs:508`, `Tools/run_hand_tracker.bat:9`, `Tools/run_hand_tracker_with_preview.bat:7` |
| `Tools/hand_landmarker_fast.task` | `HandTrackingBridge.cs:285` |
| `Tools/run_hand_tracker.bat` | `Assets/Editor/MagicTouchSceneSetup.cs:430` |
| `Tools/hand-motion-trace.txt` | `Assets/Editor/HandMotionTrace.cs:35` |
| `Tools/drawing-editor-command.txt` | `Assets/Editor/DrawingRepairChecks.cs:14` |
| `hand_landmarker.task` (thư mục gốc) | `Tools/mediapipe_streamer.py:183-187`: tìm `Tools/../hand_landmarker.task` trước, rồi `Tools/`, rồi thư mục hiện tại |
| `Tools/requirements.txt` | `Tools/setup_python_env.bat:12` |
| `Logs/` (ghi kết quả) | `mediapipe_streamer.py:315` (`Logs/tracking-performance.json`), `DrawingRepairChecks.cs:15`, `HandMotionTrace.cs:83` |

## C. Kiến trúc / pipeline (chế độ AR)

1. **`Tools/mediapipe_streamer.py`:**
   - Mở webcam `--camera` (mặc định 0, 640x480). Trên Windows thử `cv2.CAP_DSHOW` trước, lỗi thì `CAP_MSMF` (dòng 59-65).
   - Chạy MediaPipe HandLandmarker, tối đa 2 tay. Bản mediapipe 1.x không còn `mp.solutions`, nên dùng adapter Tasks (dòng 159-254). Model đọc qua buffer.
   - Gửi JSON landmark tới UDP `127.0.0.1:9050` (dòng 27-28). Nếu có `--stream-video` thì gửi JPEG tới UDP `9051` (dòng 29), kèm tiền tố session.
2. **`HandTrackingBridge.cs`** (GameObject `HandTrackingBridge`, `DefaultExecutionOrder(-100)`):
   - Nếu bật `_autoLaunchPythonStreamer` (scene: bật) thì khởi chạy streamer bằng `_pythonExecutable` (mặc định `"pythonw"`; nếu lỗi thì thử `"python"`), với `--session-id` mới (dòng 273-324).
   - Luồng nền nhận UDP 9050 (dòng 382-458). Gói sai session-id bị bỏ (`TrackingStreamSession.cs`).
   - Lọc One Euro / palm-relative và chặn spike đầu ngón (dòng 546-565). `PinchTransitionFilter` với ngưỡng vào 0.04 / ra 0.08.
   - Khi nhận gói đầu tiên thì gọi `GameManager.StartGame()` (handshake, dòng 250-256).
3. **`MediaPipeUDPReceiver.cs`** (cùng GameObject): nhận video 9051 rồi đưa vào RawImage `CameraBackground`. Không mở thêm luồng nhận landmark nếu bridge đã nhận (dòng 179-182). Có cờ `_autoLaunchPythonOnPlay` (scene: bật), nhưng khi bridge tồn tại thì việc khởi chạy được giao lại cho bridge (dòng 495-499).
4. **`DrawingManager.cs`** (GameObject `DrawingManager`):
   - Đọc trạng thái tay từ bridge mỗi frame và vẽ `LineRenderer`.
   - Chờ khi mất tracking 0.14 s; chờ nét tiếp theo `multiStrokeDelay` 0.35 s (giới hạn 0.20–0.45).
   - Nhận diện qua `RecognizePoints` (dòng 406-413).
   - **Mặc định là `SentisRecognizer`** (`RecognizerEngine.SentisNeuralNetwork`, scene `_engine: 0`). Nếu model không nạp được thì dùng `PRecognizer` ($P). `PRecognizer` cũng làm cổng hình học khi quyết định chấm sớm (dòng 415-425).
   - **`OneDollarRecognizer` không được dùng ở đâu** (chỉ có định nghĩa class). Hàm snapping `TryGetMagneticSnapPoint` có định nghĩa nhưng **không được gọi**.
5. **`DrawingManager.OnGestureRecognized`** (dòng 975) → **`GameManager.HandleGestureRecognized`** (dòng 622) → **`OnSymbolRecognized`** (dòng 659). Ở Versus thì đi qua `OnPlayerSymbolRecognized`, xác định người chơi theo điểm đầu nét. Cuối cùng gọi **`EnemyBalloon.Pop()`** (`GameManager.cs:730`, `EnemyBalloon.cs:307`) cho mọi bóng trùng ký hiệu.
6. **Chế độ nhập:**
   - Scene trong repo mặc định là **AR**: `_enableEditorSimulation: 0` trên bridge và DrawingManager, `CameraBackground._useMediaPipeStream: 1`.
   - **Mouse Simulation** bật mô phỏng trên cả hai component và chuyển nền sang `WebCamTexture` của Unity (`MagicTouchSceneSetup.cs:457-488`).
   - Giả lập chuột chỉ có trong Editor (`#if UNITY_EDITOR`, `HandTrackingBridge.cs:232-243`).

## D. Bài học kỹ thuật còn đúng

1. **Đọc model MediaPipe bằng buffer, không dùng đường dẫn.** Đường dẫn Unicode (thư mục tiếng Việt có dấu) làm tầng C++ của MediaPipe `abort()`. Code: `Tools/mediapipe_streamer.py:193-195` (`model_asset_buffer`). Nguồn: `Related/Docs/anti-4-10.readme`, mục "Lỗi 1".
2. **Ưu tiên DSHOW, MSMF là phương án dự phòng.** MSMF có thể treo khoảng 38 s khi dò webcam. Đánh đổi đo được: DSHOW 640x480 chỉ ~15 FPS, MSMF ~30 FPS. Code: `mediapipe_streamer.py:59-65`. Nguồn: `anti-4-10.readme`, mục "Lỗi 2"; `Related/Docs/fps-validation.md`.
3. **Thời gian chờ khi mất tracking: 140 ms** (`DrawingManager.cs:37`, scene 0.14). **Không còn mốc "80 ms" khi nhả tay:** code hiện tại xác nhận việc nhả tay theo *mẫu* (`PinchTransitionFilter.cs`): một mẫu mở tay chưa rõ ràng chỉ tạm dừng nét, mẫu kế tiếp mới xác nhận. Nguồn: `fps-validation.md` (ghi 80/140 ms; chỉ 140 ms còn khớp code).
4. **FPS đo thật:**
   - MSMF ~30 FPS camera.
   - Play Mode với 1 tay: camera 30,24 / tracking 28,75 FPS / inference 29,4 ms (`fps-validation.md`).
   - Chạy thử ngày 2026-10-16 (DSHOW): camera ~10–15 FPS, tracking ~10 FPS.
   - Chỉ số trực tiếp: Inspector `HandTrackingBridge > Measured Camera Performance` và `Logs/tracking-performance.json`.
5. **Chạy thử bằng chuột:** `Tools > Magic Touch > Switch Input > Mouse Simulation (PC Debug)`, Play, giữ chuột trái để vẽ. Nhớ là lệnh này tự lưu scene (luật A1). Nguồn: `anti-4-10.readme`, "Cách 2"; `MagicTouchSceneSetup.cs:450`.

## E. Hiểu lầm cần tránh

1. **Python tracker CHỈ chạy trên PC.** Unity khởi chạy nó bằng `Process.Start("pythonw")`. Trên tablet/Android phải chơi bằng cảm ứng, hoặc tracking ngay trong Unity. `Assets/Scripts/Tracking/NativeMediaPipeBridge.cs` chỉ là khung dự kiến cho mobile và **không có trong scene**. Chưa có bản build Android.
2. **Unity dùng Python nào? — VẤN ĐỀ ĐANG MỞ.**
   - Unity gọi `pythonw` trên PATH (`HandTrackingBridge.cs:51,293`; `MediaPipeUDPReceiver.cs:516`). Trên máy gốc đó là Python 3.14, thiếu `numpy`, nên streamer chết ngầm và game đứng ở "Waiting for Camera Setup...".
   - Không có code nào dùng `.venv` hay conda, dù `setup_python_env.bat:19` ghi ngược lại.
   - Môi trường conda `magictouch` (Python 3.11.17, mediapipe 1.1.1, opencv 5.0.0.93, numpy 2.4.6) chạy streamer được khi chạy riêng; **chưa kiểm tra end-to-end với Unity**.
3. **Chạy streamer bằng tay khi auto-launch còn bật thì Unity bỏ qua dữ liệu.** `TrackingStreamSession.Begin()` (`HandTrackingBridge.cs:287`) đặt session-id ngay cả khi process Python lỗi. Gói không có session-id đó bị bỏ (`TrackingStreamSession.cs`, `Matches`). Phải tắt *Auto Launch Python Streamer* và *Auto Launch Python On Play* khi chạy tay.
4. **Không chạy lại `setup_python_env.bat` đè lên `.venv` cũ tạo bằng phiên bản Python khác.** Làm vậy sẽ khiến `pyvenv.cfg` và thư viện lệch phiên bản (`numpy._core._multiarray_umath` lỗi).
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
| Python 3.13 | Không ghim; máy gốc có 3.14 (hệ thống) và 3.11 (conda `magictouch`) | `anti-4-10.readme` |
| Unity 2022.3 | Unity 6000.5.6f1 | README cũ |
| Coyote Time 0.08 s; nối nét 0.5 s | Chờ mất tracking 0.14 s; chờ nét kế 0.35 s | README cũ |
| Snapping "hút nam châm" nét vẽ; ký tự "A" | Hàm snap không được gọi; 10 ký hiệu không có "A" | README cũ |
| Quái Knight, Gargoyle, Golem | Sprite trong scene: Bat, Knight, Robot | README cũ |
| 80 ms visible-release grace | Xác nhận nhả tay theo mẫu (`PinchTransitionFilter`) | `fps-validation.md` |
