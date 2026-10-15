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
| `Assets/_Recovery/` (+ `.meta`) | **CHƯA DỜI**, xem mục 5 |

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
| Validate UI Bindings / Interaction Repair / Drawing Repair | ⏳ **Chưa chạy được**: Unity từ chối lệnh qua MCP (*"User interactions are not supported for MCP tool calls"*) |
| Play thử SampleScene (Missing Script / Prefab / sprite / âm thanh) | ⏳ **Chưa chạy được**, cùng lý do |

## 5. Việc cần bạn quyết / làm

1. **Đóng Unity để tôi dời `Assets/_Recovery/`.** Lưu ý: Unity đang có thay đổi scene **chưa lưu** (dấu `*` trên tiêu đề). Bạn quyết định lưu hay bỏ; nếu lưu thì nội dung sẽ khác với snapshot `b9defdc`.
2. Sau khi mở lại Unity, chạy 3 menu kiểm tra (`Tools/MagicTouch/UI/Validate UI Bindings`, `Tools/MagicTouch/Validate Interaction Repair`, `Tools/MagicTouch/Validate Drawing Repair`) và Play thử. Một cách khác là cho phép lệnh MCP trong Unity để tôi tự chạy.
3. `MagicTouch.slnx` vẫn đang được git theo dõi dù đã có trong `.gitignore` (`*.slnx`). Có muốn bỏ theo dõi (`git rm --cached`) không?
4. Các thư mục `Logs/*Baseline` / `*Patches` và file `Logs/assistant-perf.jsonl` (487 MB) đang được giữ nguyên theo yêu cầu.

## 6. Việc cần làm sau (chỉ báo cáo, chưa sửa)

- `Assets/Editor/MagicTouchSceneSetup.cs:551-552` tham chiếu `Assets/Sprites/Enemies/Enemy.png` và `Enemy2.png`, nhưng **hai file này không tồn tại**.
- `Assets/Scripts/Gameplay/BalloonPopEffect.cs:194` gọi `Resources.Load<Sprite>(spriteName)` khi build, nhưng dự án **không có thư mục `Resources/`**. Trong Editor, sprite VFX được nạp qua `AssetDatabase` nên vẫn chạy; khi build có thể bị trống.
- (Từ lần kiểm tra lỗi trước) 47 listener `RestartGame` trùng và việc gán lại `gameOverPanel` trong scene; Unity gọi `pythonw` của Python 3.14 (thiếu numpy) thay vì `.venv`.
- Giữ lại cho hệ thống 3 map (chưa có nơi nào dùng): `ClockTowerBackground.png`, `HalloweenBackground.png`, `CombatClockMusic.mp3`, `MagicTitle.mat`, `WizardPortrait.asset`, `Editor/UnityMCPBridge.cs`.
