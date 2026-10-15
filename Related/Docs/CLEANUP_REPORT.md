# CLEANUP REPORT — AR Magic Touch

- Ngày: 2026-10-16
- Branch: `cleanup/organize`, tách từ `QuangDai_Feature` sau 2 commit chuẩn bị
- Quy tắc đã giữ: không xoá vĩnh viễn file nào; không sửa `.cs`; không động vào `Packages/` và `ProjectSettings/`

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
| `08f9fda` | Ngừng theo dõi `MagicTouch.slnx` (`git rm --cached`; file vẫn còn trên máy, đã khớp `*.slnx` trong `.gitignore`) |
| `a0a39a1` | `.gitignore`: thêm `.idea/`. Giờ toàn bộ thư mục tự tạo lại được (Library, Temp, obj, Logs, UserSettings, .vs, .idea, *.csproj, *.sln, *.slnx) đều đã có trong `.gitignore` |

Về scene trong snapshot: **46 listener `RestartGame` trùng nhau đã có sẵn ở HEAD cũ (`52a6f5a`)**. Bản đang sửa thêm 1 listener (thành 47) và đổi `GameManager.gameOverPanel` sang `GameUI_Canvas/GameOverPanel`. Snapshot ghi lại y nguyên các thay đổi này, không sửa gì.

## 2. Bảng cũ → mới

| Đường dẫn cũ | → Mới | Lý do |
|---|---|---|
| `requirements.txt` | `Tools/requirements.txt` | Gom core Python về một chỗ. Chỉ có 1 file nên không cần hợp nhất |
| `Tools/setup_python_env.bat` dòng 12 | `-r requirements.txt` → `-r Tools\requirements.txt` | Script `cd` về thư mục gốc rồi mới gọi pip. Đã chạy thử `pip --dry-run` thành công |
| `anti-4-10.readme` | `Related/Docs/anti-4-10.readme` | Nhật ký phiên làm việc |
| `design_guidelines.md` | `Related/Design/design_guidelines.md` | Định hướng UX/art |
| `Logs/phase9-*.png` (10 file) | **copy** → `Related/Reference-Art/` | Ảnh tham chiếu UI. Bản gốc trong `Logs/` vẫn còn |
| `Logs/*.csv`, `Logs/*.json` (42 file) | **copy** → `Related/Data-Samples/` (git-ignored) | Dữ liệu chuyển động tay / profile |
| `Logs/fps-validation.md` | **copy** → `Related/Docs/` | Số đo FPS thật |
| — | `Related/Logic-Notes/.gitkeep` | Thư mục trống theo cấu trúc yêu cầu |

## 3. Danh sách trong `Related/_ToDelete/` (git-ignored, vẫn còn trong lịch sử git)

| File | Lý do |
|---|---|
| `anti-4/10.readme` | Trùng từng byte với `anti-4-10.readme` (cùng SHA-256 `AED3BAA0…`) |
| `agent_rules.md`, `project_brief.md` | Lấy lại từ commit `52a6f5a`. Phần lớn đã lỗi thời ($1 Unistroke, MediaPipe Unity Plugin, chụm ngón giữa, Object Pooling, `BRIEF.md`). Phần còn đúng đã gom vào `Notes_for_CLAUDE_AGENTS.md` |
| `Feature` | File rỗng (0 byte) |
| `Tools/unity-rpc.cjs`, `Tools/unity-request.json`, `Tools/focus-drawing-editor.ps1` | Script tự động hoá do agent để lại; không có code game nào tham chiếu. Không chứa bí mật viết cứng (token được đọc lúc chạy từ `Library/McpUnity/bridge-token`) |
| `Assets/_Recovery/0.unity`, `0 (1).unity` (+ `.meta` và `_Recovery.meta`) | Bản phục hồi tự động cũ (9 và 23 object, so với SampleScene hiện tại 622 KB), không có script nào mà SampleScene thiếu, 0 tham chiếu GUID. Đã dời ở commit `322609b` |

## 4. Kết quả kiểm tra (Pha C)

| Kiểm tra | Kết quả |
|---|---|
| Đường dẫn gán cứng: `Tools/mediapipe_streamer.py`, `hand_landmarker_fast.task`, `run_hand_tracker.bat`, `hand-motion-trace.txt`, `drawing-editor-command.txt` | ✅ Còn nguyên |
| `hand_landmarker.task` (gốc) | ✅ Còn nguyên. Streamer vẫn tìm thấy (`Tools/../hand_landmarker.task`) |
| Unit test Python (`test_stream_protocol`, `test_hand_identity`) | ✅ 9/9 pass |
| `pip install -r Tools/requirements.txt --dry-run` bằng `.venv` | ✅ Chạy được |
| Quét GUID mồ côi: 49 file `.unity/.prefab/.asset/.mat/...` trong `Assets/` và `ProjectSettings/`, đối chiếu 22.415 file `.meta` (Assets + PackageCache) | ⚠️ Có 8 GUID mồ côi, **tất cả có từ commit đầu tiên `0fa359f`, không do đợt dọn này**: 7 GUID trong `Assets/Settings/Renderer2D.asset` (shader/texture debug Probe Volume và `m_FallOffLookup`) và 1 GUID trong `TMP Settings.asset` (`m_defaultSpriteAsset`, sprite emoji mặc định của TMP chưa được import) |
| Unity Console | ✅ Không có lỗi hay warning mới. Còn lỗi cũ: `UnityMCPBridge` trùng cổng 8080, `mcp-unity` trùng cổng 8090, ký tự ❤ thiếu trong font `LivesText` |
| Biên dịch | ✅ Không có file `.cs` nào bị đổi hay dời. Console không có lỗi biên dịch |
| Validate UI Bindings (bạn chạy lúc 02:37:26) | ✅ `PASS: 24 buttons, pixel fonts, panel references and 9-slice sprites` |
| Validate Interaction Repair (02:37:30) | ✅ `PASS: jitter ratio=0.020; …multi-stroke X/E, loss and reset`. Kèm 4 lỗi đỏ `DrawingTrail_Hand_1_Stroke_0..3: Destroy may not be called from edit mode`. Đây là nhiễu có sẵn từ trước: validator gọi `DrawingManager.ClearAllLines()` (dùng `Destroy`, `DrawingManager.cs:942`) trong Edit Mode, trên một object tạm `HideAndDontSave`. Các nét vẽ là con của object tạm đó và bị xoá bằng `DestroyImmediate(root)` ở cuối (`InteractionRepairChecks.cs:186`). Scene không có `DrawingTrail_*` nào bị sót lại. Đợt dọn không sửa file `.cs` nào (`git diff 52a6f5a HEAD -- *.cs` rỗng) |
| Validate Drawing Repair (02:37:34) | ✅ `PASS: arc length, degenerate strokes, spline bounds, fast motion, fresh samples, start/recovery noise, player isolation` |
| Play thử SampleScene (Missing Script / Prefab / sprite / âm thanh) | ⏳ Chưa có kết quả |
| Scene sau khi mở lại Unity | ⚠️ `SampleScene.unity` bị **tự lưu lúc 02:37:10**, thêm listener `RestartGame` thứ 48. Chưa commit; xem mục 6 |

## 5. Việc cần bạn quyết / làm

1. ~~Đóng Unity để dời `Assets/_Recovery/`~~: **đã xong** (`322609b`).
2. **Còn lại cho bạn:** mở lại Unity (lần đầu Unity sẽ ghi nhận việc `_Recovery` bị gỡ), rồi chạy 3 menu kiểm tra (`Tools/MagicTouch/UI/Validate UI Bindings`, `Tools/MagicTouch/Validate Interaction Repair`, `Tools/MagicTouch/Validate Drawing Repair`) và Play thử SampleScene. Kết quả được ghi vào Console và `Logs/drawing-repair-result.json`.
3. ~~`MagicTouch.slnx` vẫn được git theo dõi~~: **đã ngừng theo dõi** (`08f9fda`).
4. **Kiểm tra trước khi push lần đầu** (trên `cleanup/organize` @ `08f9fda`):
   - `git log --all --oneline -- .venv`: **rỗng** (0 commit).
   - Không có file nào nằm dưới `.env`, `.venv/`, `Related/_ToDelete/`, `Related/Data-Samples/` trong `git ls-files`. Cũng không có file `.env` nào trong dự án.
   - 10 file lớn nhất đang theo dõi: `CombatCastleMusic.wav` 19,81 MB; `hand_landmarker.task` 7,46 MB; `Tools/hand_landmarker_fast.task` 7,12 MB; `Lit2DSceneTemplate.scenetemplate` 3,76 MB; `HalloweenBackground.png` 2,35 MB; `ClockTowerBackground.png` 2,34 MB; `LiberationSans SDF.asset` 2,15 MB; `CastleBackground.png` 2,05 MB; `PressStart2P SDF.asset` 2,04 MB; `phase9-menu-1920x1080.png` 1,58 MB. Tất cả đều dưới ngưỡng cảnh báo 50 MB của GitHub. Pack của repo: 51,88 MiB.
5. Merge `cleanup/organize` (merge thường, không squash) và push: **chờ bạn xác nhận Play thử ổn**.
6. Các thư mục `Logs/*Baseline` / `*Patches` và file `Logs/assistant-perf.jsonl` (487 MB) đang được giữ nguyên theo yêu cầu.

## 6. Việc cần làm sau (chỉ báo cáo, chưa sửa)

- `Assets/Editor/MagicTouchSceneSetup.cs:551-552` tham chiếu `Assets/Sprites/Enemies/Enemy.png` và `Enemy2.png`, nhưng **hai file này không tồn tại**.
- `Assets/Scripts/Gameplay/BalloonPopEffect.cs:194` gọi `Resources.Load<Sprite>(spriteName)` khi build, nhưng dự án **không có thư mục `Resources/`**. Trong Editor, sprite VFX được nạp qua `AssetDatabase` nên vẫn chạy; khi build có thể bị trống.
- **Nguyên nhân scene tự sinh listener và tự đổi `gameOverPanel`:** `MagicTouchSceneSetup` là `[InitializeOnLoad]` (`Assets/Editor/MagicTouchSceneSetup.cs:23-45`). Mỗi lần domain reload (mở Unity hoặc compile lại), nó kiểm tra `GameObject.Find("GameOverPanel") == null`. Nhưng `GameObject.Find` chỉ tìm được object **đang bật**, mà `GameUI_Canvas/GameOverPanel` được lưu ở trạng thái **tắt**, nên điều kiện luôn đúng. Kết quả là `GenerateGameOverUIInternal` chạy lại mỗi lần:
  - `btn.onClick.RemoveAllListeners()` (dòng 277) không gỡ được listener *persistent*, rồi `AddPersistentListener` (dòng 278) thêm 1 listener mới, nên 46 → 47 → 48.
  - Dòng 295 gán lại `GameManager.gameOverPanel` sang panel cũ.
  - Cuối cùng scene bị `SaveScene` tự động.

  Lỗi panel Game Over chồng nhau và listener trùng đều bắt nguồn từ đây, không phải do người sửa tay.
- (Từ lần kiểm tra lỗi trước) Listener `RestartGame` trùng và việc gán lại `gameOverPanel` trong scene; Unity gọi `pythonw` của Python 3.14 (thiếu numpy) thay vì `.venv`.
- Giữ lại cho hệ thống 3 map (chưa có nơi nào dùng): `ClockTowerBackground.png`, `HalloweenBackground.png`, `CombatClockMusic.mp3`, `MagicTitle.mat`, `WizardPortrait.asset`, `Editor/UnityMCPBridge.cs`.
