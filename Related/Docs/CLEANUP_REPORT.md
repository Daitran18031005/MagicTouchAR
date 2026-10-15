# CLEANUP REPORT — AR Magic Touch

- Ngày: 2026-10-16
- Branch làm việc: `cleanup/organize`, tách từ `QuangDai_Feature` sau 2 commit chuẩn bị.
- **Trạng thái cuối:** `cleanup/organize` đã được merge (fast-forward, giữ nguyên từng commit nhỏ, không squash) vào `QuangDai_Feature` và đã push lên `origin/QuangDai_Feature` (`52a6f5a..82c4e59`). `main` không bị động tới (vẫn ở `52a6f5a`). Chưa mở Pull Request.
- Quy tắc đã giữ: không xoá vĩnh viễn file nào; không sửa `.cs`; không động vào `Packages/` và `ProjectSettings/`.

## 1. Commit đã tạo

| Commit | Nội dung |
|---|---|
| `9c45f79` | `.gitignore`: thêm `.venv/`, `Related/_ToDelete/`, `Related/Data-Samples/`, `Tools/unity-rpc.cjs`, `Tools/unity-request.json` |
| `b9defdc` | Snapshot trạng thái trước khi dọn (scene đang sửa, `setup_python_env.bat`, file `Feature`, việc xoá `agent_rules.md`/`project_brief.md`) |
| `fe7f1e7` | `requirements.txt` → `Tools/requirements.txt` và sửa dòng 12 của `setup_python_env.bat` |
| `fb304f3` | Dời rác vào `Related/_ToDelete/` |
| `f240d1d` | Dời tài liệu: `anti-4-10.readme`, `design_guidelines.md` |
| `70f8b28` | Copy ảnh UI Phase 9 và `fps-validation.md` từ `Logs/` |
| `7bcfd7e` | `Related/Docs/Notes_for_CLAUDE_AGENTS.md` |
| `ac85c84` | Báo cáo này (bản đầu) |
| `322609b` | `Assets/_Recovery/` (+ 5 `.meta`) → `Related/_ToDelete/Assets/`. Dời khi Unity đã đóng hẳn; trước khi dời, working tree sạch |
| `2ed42ac` | Cập nhật báo cáo sau khi dời `_Recovery` |
| `1b856c8` | Ghi kết quả 3 bộ kiểm tra Pha C và nguyên nhân scene tự lưu |
| `a0a39a1` | `.gitignore`: thêm `.idea/`. Giờ toàn bộ thư mục tự tạo lại được (Library, Temp, obj, Logs, UserSettings, .vs, .idea, *.csproj, *.sln, *.slnx) đều đã có trong `.gitignore` |
| `da6eb2c` | Ghi việc thêm `.idea/` vào báo cáo |
| `08f9fda` | Ngừng theo dõi `MagicTouch.slnx` (`git rm --cached`; file vẫn còn trên máy, đã khớp `*.slnx` trong `.gitignore`) |
| `82c4e59` | Ghi việc ngừng theo dõi slnx và kết quả kiểm tra trước khi push |

Sau đó có thêm các commit chỉ sửa tài liệu (README, báo cáo này, Notes), mỗi file một commit.

Về scene trong snapshot `b9defdc`: **46 listener `RestartGame` trùng nhau đã có sẵn ở HEAD cũ (`52a6f5a`)**. Bản lúc đó đang sửa thêm 1 listener (thành 47) và đổi `GameManager.gameOverPanel` sang `GameUI_Canvas/GameOverPanel`. Snapshot ghi lại y nguyên, không sửa gì. Nguyên nhân: xem mục 7.

## 2. Bảng cũ → mới (đầy đủ)

| Đường dẫn cũ | → Mới | Lý do |
|---|---|---|
| `requirements.txt` | `Tools/requirements.txt` | Gom core Python về một chỗ. Chỉ có 1 file nên không cần hợp nhất |
| `Tools/setup_python_env.bat` dòng 12 | `-r requirements.txt` → `-r Tools\requirements.txt` | Script `cd` về thư mục gốc rồi mới gọi pip. Đã chạy thử `pip --dry-run` thành công (lúc đó `.venv` còn tốt) |
| `anti-4-10.readme` | `Related/Docs/anti-4-10.readme` | Nhật ký phiên làm việc |
| `anti-4/10.readme` | `Related/_ToDelete/anti-4/10.readme` | Bản trùng |
| `design_guidelines.md` | `Related/Design/design_guidelines.md` | Định hướng UX/art |
| `agent_rules.md`, `project_brief.md` (đã bị xoá trong working tree) | Lấy lại từ `52a6f5a` → `Related/_ToDelete/` | Lỗi thời; phần còn đúng gom vào Notes |
| `Feature` (0 byte) | `Related/_ToDelete/Feature` | Rác |
| `Tools/unity-rpc.cjs`, `Tools/unity-request.json`, `Tools/focus-drawing-editor.ps1` | `Related/_ToDelete/Tools/` | Script agent để lại |
| `Assets/_Recovery/` + `.meta` | `Related/_ToDelete/Assets/_Recovery/` | Bản phục hồi tự động cũ |
| `Logs/phase9-*.png` (10 file) | **copy** → `Related/Reference-Art/` | Ảnh tham chiếu UI. Bản gốc trong `Logs/` vẫn còn |
| `Logs/*.csv`, `Logs/*.json` (42 file) | **copy** → `Related/Data-Samples/` (git-ignored) | Dữ liệu chuyển động tay / profile |
| `Logs/fps-validation.md` | **copy** → `Related/Docs/` | Số đo FPS thật |
| — | `Related/Logic-Notes/.gitkeep` | Thư mục trống theo cấu trúc yêu cầu |
| `MagicTouch.slnx` | GIỮ trên máy, **ngừng theo dõi** trong git | File do IDE/Unity tự tạo |
| `.gitignore` | thêm `.venv/`, `.idea/`, `Related/_ToDelete/`, `Related/Data-Samples/`, 2 script agent | Không đưa môi trường và file tạm lên git |

## 3. Danh sách trong `Related/_ToDelete/` (git-ignored, CHƯA xoá, vẫn còn trong lịch sử git)

| File | Lý do |
|---|---|
| `anti-4/10.readme` | Trùng từng byte với `anti-4-10.readme` (cùng SHA-256 `AED3BAA0…`) |
| `agent_rules.md`, `project_brief.md` | Lấy lại từ commit `52a6f5a`. Phần lớn đã lỗi thời ($1 Unistroke, MediaPipe Unity Plugin, chụm ngón giữa, Object Pooling, `BRIEF.md`). Phần còn đúng đã gom vào `Notes_for_CLAUDE_AGENTS.md` |
| `Feature` | File rỗng (0 byte) |
| `Tools/unity-rpc.cjs`, `Tools/unity-request.json`, `Tools/focus-drawing-editor.ps1` | Script tự động hoá do agent để lại; không có code game nào tham chiếu. Không chứa thông tin xác thực viết cứng (thông tin xác thực chỉ được đọc lúc chạy từ một file trong `Library/`) |
| `Assets/_Recovery/0.unity`, `0 (1).unity` (+ `.meta` và `_Recovery.meta`) | Bản phục hồi tự động cũ (9 và 23 object, so với SampleScene hiện tại 622 KB), không có script nào mà SampleScene thiếu, 0 tham chiếu GUID. Đã dời ở commit `322609b` |

## 4. Quyết định đã chốt

- **Giữ** (dành cho hệ thống 3 map, hiện chưa nơi nào dùng): `Assets/Sprites/Background/ClockTowerBackground.png`, `HalloweenBackground.png`, `Assets/Audio/CombatClockMusic.mp3`, `Assets/UI/Phase9/MagicTitle.mat`, `WizardPortrait.asset`.
- **Giữ** `Assets/Editor/UnityMCPBridge.cs` (dù gây lỗi trùng cổng 8080 trong Console).
- **Không dời** `hand_landmarker.task` (ở gốc), `Train/`, `README.md`, `Logs/`, `ignore.conf`, `.vsconfig`, và toàn bộ core trong `Tools/`.
- **Ngừng theo dõi** `MagicTouch.slnx`.
- **Thêm vào `.gitignore`:** `.venv/` và `.idea/` (cùng `Related/_ToDelete/`, `Related/Data-Samples/`, 2 script agent).
- Merge vào `QuangDai_Feature` (không phải `main`), merge thường, không squash, không force push.

## 5. Kết quả kiểm tra (Pha C)

| Kiểm tra | Kết quả |
|---|---|
| Đường dẫn gán cứng: `Tools/mediapipe_streamer.py`, `hand_landmarker_fast.task`, `run_hand_tracker.bat`, `hand-motion-trace.txt`, `drawing-editor-command.txt` | ✅ Còn nguyên |
| `hand_landmarker.task` (gốc) | ✅ Còn nguyên. Streamer vẫn tìm thấy (`Tools/../hand_landmarker.task`) |
| Unit test Python (`test_stream_protocol`, `test_hand_identity`) | ✅ 9/9 pass |
| `pip install -r Tools/requirements.txt --dry-run` bằng `.venv` | ✅ Chạy được tại thời điểm dọn. Sau đó `.venv` bị hỏng (xem mục 7) |
| Quét GUID mồ côi: 47 file `.unity/.prefab/.asset/.mat/...` trong `Assets/` và `ProjectSettings/` (sau khi gỡ `_Recovery`), đối chiếu 22.412 file `.meta` (Assets + PackageCache) | ⚠️ Có 8 GUID mồ côi, **tất cả có từ commit đầu tiên `0fa359f`, không do đợt dọn này**: 7 GUID trong `Assets/Settings/Renderer2D.asset` (shader/texture debug Probe Volume và `m_FallOffLookup`) và 1 GUID trong `TMP Settings.asset` (`m_defaultSpriteAsset`, sprite emoji mặc định của TMP chưa được import) |
| Unity Console | ✅ Không có lỗi hay warning mới. Còn lỗi cũ: `UnityMCPBridge` trùng cổng 8080, `mcp-unity` trùng cổng 8090, ký tự ❤ thiếu trong font `LivesText` |
| Biên dịch | ✅ Không có file `.cs` nào bị đổi hay dời (`git diff 52a6f5a HEAD -- *.cs` rỗng). Console không có lỗi biên dịch |
| Validate UI Bindings | ✅ `PASS: 24 buttons, pixel fonts, panel references and 9-slice sprites` |
| Validate Interaction Repair | ✅ `PASS: jitter ratio=0.020; …multi-stroke X/E, loss and reset`. Kèm 4 lỗi đỏ `DrawingTrail_Hand_1_Stroke_0..3: Destroy may not be called from edit mode`. Đây là nhiễu có sẵn từ trước: validator gọi `DrawingManager.ClearAllLines()` (dùng `Destroy`) trong Edit Mode, trên một object tạm `HideAndDontSave`. Các nét vẽ là con của object tạm đó và bị xoá bằng `DestroyImmediate(root)` ở cuối (`InteractionRepairChecks.cs:186`). Scene không có `DrawingTrail_*` nào bị sót lại |
| Validate Drawing Repair | ✅ `PASS: arc length, degenerate strokes, spline bounds, fast motion, fresh samples, start/recovery noise, player isolation` |
| Play thử SampleScene (Missing Script / Prefab / sprite / âm thanh) | ⏳ **Chưa kiểm tra** (chưa có kết quả Play) |
| Scene trên máy (chưa commit) | ⚠️ `SampleScene.unity` bị tự lưu khi mở lại Unity (thêm listener `RestartGame` thứ 48). Sau đó lại bị lưu khi chọn `Switch Input > Mouse Simulation`: `_enableEditorSimulation: 1` ×2, `_useMediaPipeStream: 0`. Bản trong repo vẫn ở chế độ AR. Đang giữ chưa commit theo quyết định của chủ dự án |

## 6. Kiểm tra trước khi push

Kiểm tra trên toàn bộ 15 commit đã push (`origin/QuangDai_Feature` `52a6f5a..82c4e59`):

- `git log --all --oneline -- .venv`: **rỗng**. Không có đường dẫn `.venv/` hay `.env` nào trong lịch sử của bất kỳ nhánh nào.
- `git ls-files`: 0 file dưới `.env`, `.venv/`, `Related/_ToDelete/`, `Related/Data-Samples/`.
- Quét thông tin nhạy cảm (khóa API, mật khẩu, khóa riêng, các tiền tố khóa GitHub/OpenAI/AWS…) trên các dòng được thêm: không có. Chỉ khớp một câu mô tả trong báo cáo này.
- **10 file (blob) lớn nhất trong lịch sử của `QuangDai_Feature`:**

| Kích thước | File |
|---|---|
| 19,81 MB | `Assets/Audio/CombatCastleMusic.wav` |
| 7,46 MB | `hand_landmarker.task` |
| 7,12 MB | `Tools/hand_landmarker_fast.task` |
| 4,21 MB | `Assets/Sprites/Background/HalloweenBackground.png` (phiên bản cũ) |
| 4,18 MB | `Assets/Sprites/Background/ClockTowerBackground.png` (phiên bản cũ) |
| 3,76 MB | `Assets/Settings/Lit2DSceneTemplate.scenetemplate` |
| 3,67 MB | `Assets/Sprites/Background/CastleBackground.png` (phiên bản cũ) |
| 2,35 MB | `Assets/Sprites/Background/HalloweenBackground.png` |
| 2,34 MB | `Assets/Sprites/Background/ClockTowerBackground.png` |
| 2,15 MB | `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset` |

  Tất cả đều dưới ngưỡng cảnh báo 50 MB của GitHub. File mới lớn nhất trong 15 commit là `Related/Reference-Art/phase9-menu-1920x1080.png` (1,58 MB).
- Các thư mục `Logs/*Baseline` / `*Patches` và file `Logs/assistant-perf.jsonl` (487 MB) được giữ nguyên trên máy (`Logs/` bị .gitignore).

## 7. Việc cần làm sau (chỉ báo cáo, CHƯA sửa)

| Việc | Trạng thái | Chi tiết |
|---|---|---|
| Unity chọn sai Python cho AR | ❌ Chưa xử lý | `HandTrackingBridge.cs:51,293` dùng `pythonw` trên PATH (máy gốc: Python 3.14 thiếu `numpy`). Không có code nào dùng `.venv`/conda, dù `setup_python_env.bat:19` ghi "the game will use .venv automatically". Môi trường conda `magictouch` chạy streamer được khi chạy riêng |
| `.venv` hỏng | ❌ Chưa xử lý | Chạy lại `setup_python_env.bat` đè lên `.venv` cũ (tạo bằng Python 3.11), khiến `pyvenv.cfg` trỏ sang Python 3.14 trong khi thư viện vẫn là bản `cp311`. Import báo `No module named 'numpy._core._multiarray_umath'` |
| `Enemy.png` / `Enemy2.png` không tồn tại | ❌ Chưa xử lý | `Assets/Editor/MagicTouchSceneSetup.cs:551-552` |
| `Resources.Load` khi không có thư mục `Resources/` | ❌ Chưa xử lý | `Assets/Scripts/Gameplay/BalloonPopEffect.cs:194`. Trong Editor, sprite VFX được nạp qua `AssetDatabase`; khi build có nguy cơ mất sprite VFX (chưa kiểm tra build) |
| Listener `RestartGame` trùng trong scene | ❌ **Chưa xử lý** | Bản trong repo có 47 listener trên `GameUI_Canvas/GameOverPanel/RestartButton`; bản trên máy có 48. **Nguyên nhân:** `MagicTouchSceneSetup` là `[InitializeOnLoad]` (`MagicTouchSceneSetup.cs:23-45`). Mỗi lần domain reload, nó kiểm tra `GameObject.Find("GameOverPanel") == null`, nhưng `GameObject.Find` không thấy object đang tắt nên điều kiện luôn đúng. Kết quả là `GenerateGameOverUIInternal` chạy lại mỗi lần: `RemoveAllListeners()` (dòng 277) không gỡ listener persistent, `AddPersistentListener` (dòng 278) thêm 1 listener, dòng 295 gán lại `GameManager.gameOverPanel` sang panel cũ, rồi scene bị `SaveScene` tự động. Lỗi hai panel Game Over chồng nhau cũng từ đây |
| Chuyển nhạc `.wav` sang `.ogg` | ❌ Chưa làm | `CombatCastleMusic.wav` 19,81 MB là file lớn nhất repo; các SFX khác cũng là `.wav` |
