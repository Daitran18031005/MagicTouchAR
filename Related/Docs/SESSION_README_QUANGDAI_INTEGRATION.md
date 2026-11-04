# 🪄 NHẬT KÝ PHIÊN LÀM VIỆC & TÍCH HỢP HỆ THỐNG (04/11/2026)
**Dự án:** Magic Touch AR (`MagicTouchAR`)  
**Thư mục làm việc:** `D:\Hoc Tap\hk7\Công nghệ mới\MagicTouchAR-main`  
**Repository:** https://github.com/Daitran18031005/MagicTouchAR  
**Nhánh nguồn:** `QuangDai_Feature` (kết hợp `fix/tracking-performance`)  
**Tác tử hỗ trợ:** Antigravity (Google DeepMind)

---

## 📋 MỤC TIÊU PHIÊN LÀM VIỆC

1. **Khảo sát và tải nhánh `QuangDai_Feature`**: Tải mã nguồn nhánh này ra một thư mục riêng biệt để đối chiếu, đồng thời liên kết với dự án chính để test.
2. **Phân tích so sánh toàn diện**: Chỉ rõ mọi điểm khác biệt giữa nhánh `main` và `QuangDai_Feature` (Gameplay, Boss, Creep, UI, Audio, Tracking, AI Model).
3. **Thiết lập môi trường Test an toàn tuyệt đối**: Đưa toàn bộ tính năng của `QuangDai_Feature` vào workspace hiện tại mà không làm mất mát bất kỳ file nào đang làm dở của người dùng, đảm bảo có thể hoàn tác về nhánh cũ bất kỳ lúc nào.
4. **Xử lý sự cố trong Unity**:
   - Khắc phục lỗi mở nhầm scene rỗng (`Untitled`) làm màn hình Game bị xanh trống.
   - Khắc phục cơ chế kiểm tra camera khiến chế độ vẽ bằng chuột (Mouse Simulation) bị kẹt.
5. **Điều tra và khắc phục lỗi Tracking AR bị lag & đứt nét**: Phân tích nguyên nhân sâu xa vì sao nhánh `QuangDai_Feature` bị lag nặng và nét vẽ bị băm nát, sau đó tích hợp toàn bộ bản vá hiệu năng (P-core affinity, Pinch debounce, CPU Burst inference).

---

## 🛠️ CHI TIẾT CÁC BƯỚC THỰC HIỆN & GIẢI PHÁP KỸ THUẬT

### BƯỚC 1: Tải nhánh ra thư mục riêng & Thiết lập Git Remote 2 chiều
- **Thực hiện:**
  - Clone nhánh `QuangDai_Feature` từ GitHub về thư mục độc lập:  
    `D:\Hoc Tap\hk7\Công nghệ mới\MagicTouchAR-QuangDai_Feature`
  - Thiết lập Git Remote cục bộ 2 chiều:
    - Tại thư mục mới: Thêm remote `local-main` trỏ về `MagicTouchAR-main`.
    - Tại thư mục chính: Thêm remote `local-quangdai` trỏ sang `MagicTouchAR-QuangDai_Feature`.
- **Kết quả:** Hai kho lưu trữ có thể fetch và đồng bộ trực tiếp trên ổ cứng mà không cần thông qua mạng Internet.

---

### BƯỚC 2: Phân tích so sánh giữa nhánh `main` và `QuangDai_Feature`
Nhánh `QuangDai_Feature` đi trước nhánh `main` **36 commits** (+1.315 files thay đổi). Các nâng cấp cốt lõi bao gồm:

1. **Hệ thống Boss & Creep mới:**
   - Thêm 2 Boss hoàn chỉnh: **Dullahan** (Kỵ sĩ không đầu) và **Ember Drake** (Rồng lửa) có đầy đủ hoạt ảnh xuất hiện hoành tráng, thanh máu, và cơ chế thi triển kỹ năng (`BossDefinition.cs`, `BossSkillCaster.cs`, `BossVisual.cs`, `SoloBossEncounter.cs`).
   - Quái nhỏ (Creep) bay theo đàn hình lượn sóng (`CreepCluster.cs`) dựa trên bảng tỉ lệ xuất hiện theo tiến trình game (`CreepSpawnTable.cs`).
   - Hiệu ứng bóng bay lắc lư tự nhiên (`BalloonSwaySet.cs`).
2. **Giao diện Retro Pixel-Art (UI Phase 9) & Chế độ Chơi:**
   - Vẽ lại toàn bộ UI theo chuẩn Arcade 16-bit với font `PressStart2P` SDF và các thành phần 9-slice (`Banner`, `Modal`, `Tablet`, `GreenButton`, `Ribbon`, `Trophy`, `Medal`...).
   - `MenuManager.cs`: Quản lý luồng giao diện hoàn chỉnh (Menu chính, Solo, Versus, Settings điều chỉnh BGM/SFX và độ khó Easy/Normal/Hard, Pause, Victory, Game Over).
   - `VersusMatch.cs`: Hỗ trợ chế độ đối kháng 2 người chơi chia đôi màn hình tính điểm theo thời gian (3/5/7 phút).
3. **Đạo diễn Âm nhạc (`MusicDirector.cs`):**
   - Tự động chuyển nhạc nền mượt mà theo trạng thái: Nhạc Menu (`menu.ogg`), Nhạc Chiến đấu (`CombatClockMusic.ogg`), Tiếng Boss gầm (`BossRoar_EmberDrake.ogg`), Nhạc Đánh Boss (`boss_combat.ogg`), Nhạc Chiến thắng (`GameVictory.ogg`).
4. **Model AI Nhận diện Ký hiệu (Sentis ONNX):**
   - Model `magic_touch_model.onnx` được fine-tune cải thiện vượt trội khả năng nhận diện dấu mũ `^` (Caret).
5. **Cơ chế Tracking AR & Môi trường chạy:**
   - Tự động tạo và cô lập môi trường ảo Python 3.11 trong `.venv` (`Tools/setup_python_env.bat`).
   - Chuyển đổi linh hoạt giữa chế độ AR và Chuột thông qua EditorPrefs, không làm sửa đổi file Scene.

---

### BƯỚC 3: Thiết lập Test an toàn & Sao lưu Workspace
- **Vấn đề cần bảo vệ:** Thư mục hiện tại có các file untracked về hoạt ảnh Pháp sư (`Wizard_Animated.prefab`, `WizardSpriteAnimator.cs`, `Wizard_Frames`, `README_HoangAnh.md`).
- **Giải pháp:**
  1. Commit toàn bộ tài nguyên trên vào nhánh sao lưu an toàn: `backup-my-work-before-test`.
  2. Chuyển thư mục hiện tại (`MagicTouchAR-main`) sang nhánh `QuangDai_Feature`.
  3. Đảm bảo trạng thái working tree luôn sạch sẽ và nhánh `main` gốc giữ nguyên vẹn 100%.

---

### BƯỚC 4: Xử lý các sự cố phát sinh trong Unity

#### 1. Sự cố màn hình Game xanh rỗng (`Untitled*` Scene)
- **Hiện tượng:** Khi người dùng mở Unity và bấm Play, màn hình Game chỉ hiện một màu xanh trơn, cột Hierarchy chỉ có `Main Camera`.
- **Nguyên nhân:** Khi Git chuyển nhánh, Unity reload và mở một Scene mặc định chưa đặt tên (`Untitled*`) thay vì mở scene game.
- **Xử lý:** Dừng Play Mode thông qua Unity MCP và mở lại đúng scene chính: `Assets/Scenes/SampleScene.unity`. Toàn bộ GameObjects (Boss, Spawner, HUD, Canvas...) đã hiển thị đầy đủ.

#### 2. Sự cố không vẽ được bằng chuột (Mouse Simulation)
- **Hiện tượng:** Chuyển sang chế độ vẽ bằng chuột nhưng khi bấm Play không thể vẽ được nét.
- **Nguyên nhân:** 
  1. Game khởi động ở `MainMenu`, nét vẽ bị khóa cho đến khi vào trạng thái `Playing`.
  2. Khi bấm `PLAY SOLO`, game chuyển sang `WaitingForCamera`. Trong code gốc, hệ thống yêu cầu một cú click "mồi" để hoàn tất handshake camera, gây hiểu nhầm là game bị đơ.
- **Xử lý:** Bổ sung logic tự động sẵn sàng trong `HandTrackingBridge.cs`:
  ```csharp
  if (mode.HasValue) _enableEditorSimulation = mode.Value == InputMode.MouseSimulation;
  if (_enableEditorSimulation) hasReceivedFirstData = true;
  ```
  Khi ở chế độ chuột, game tự động kích hoạt `hasReceivedFirstData = true` ➜ Vào game lập tức và vẽ được ngay mà không cần chờ.

---

### BƯỚC 5: Điều tra & Khắc phục triệt để lỗi Tracking AR bị lag & đứt nét

#### 1. Phân tích nguyên nhân gốc rễ
Người dùng phản ánh: Trên nhánh `QuangDai_Feature`, tracking tay rất giật lag và hầu như không thể vẽ được gì. Qua kiểm tra mã nguồn và tài liệu kỹ thuật `TRACKING_PERFORMANCE.md`, đã xác định 4 nguyên nhân:
1. **MediaPipe bị ép chạy trên E-Cores (CPU lai Intel):**
   Do Unity khởi chạy Python streamer ngầm (`pythonw.exe`), Windows 10/11 coi đây là tiến trình nền và đẩy sang các nhân tiết kiệm điện (E-cores). MediaPipe đa luồng bị nghẽn, thời gian xử lý vọt từ **19ms lên 242ms/frame**, kéo FPS camera rơi tự do xuống **chỉ còn 4 FPS** (thậm chí 0.3 – 1.4 FPS).
2. **Mất bộ lọc Pinch Debounce:**
   Commit `bfb8db5` của nhánh QuangDai chuyển việc tính chụm ngón tay sang `Tools/pinch_state.py` theo tỉ lệ `ngón tay / lòng bàn tay`, nhưng **vứt bỏ cơ chế chống rung 1 frame**. Khi camera bị lag, chỉ cần ngón tay rung nhẹ 1 milimet giữa 2 frame là Python báo nhả tay, Unity ép ngắt nét vẽ ngay lập tức. Nét vẽ bị băm vụn (< 6 điểm) khiến AI từ chối nhận diện đến 97.4%.
3. **UDP Timeout quá ngắn (0.25s):**
   Khi camera xử lý mất > 250ms/khung, Unity tưởng mất tay hoàn toàn nên liên tục xóa và nhận lại tay (`SetHandLost`), xóa luôn nét đang vẽ dở.
4. **Sentis CNN chạy trên GPU gây khựng frame:**
   Chạy model CNN 28×28 trên `GPUCompute` làm nghẽn luồng chính Unity (5.4ms – 15ms mỗi lần nhấc bút) do phải đồng bộ đọc dữ liệu ngược từ GPU về CPU.

#### 2. Tích hợp bản vá hiệu năng hoàn chỉnh
Đã tích hợp toàn bộ các giải pháp kỹ thuật từ commit `e98ec57` (`fix/tracking-performance`) vào nhánh hiện tại:
- **`Tools/cpu_affinity.py`**: Tự động nhận diện CPU lai và ghim MediaPipe vào các **P-cores (Performance Cores)** thông qua cờ `--cpu-policy auto`. Tốc độ tracking phục hồi đạt **25 – 30 FPS**.
- **`Tools/pinch_state.py` & `HandTrackingBridge.cs`**: Bổ sung cờ `pinchPending`. Khi ngón tay có tín hiệu mở không dứt khoát trong 1 frame, nét vẽ chỉ tạm dừng; khung hình kế tiếp mới xác nhận nhả tay (hoặc mở góc rộng $\ge 0.375$ mới nhả ngay). Nét vẽ liền mạch, không còn bị đứt gãy.
- **Adaptive UDP Timeout**: Điều chỉnh thời gian chờ mất gói linh hoạt theo nhịp gửi thực tế của camera (tối đa 0.75s).
- **`SentisRecognizer.cs`**: Chuyển chế độ suy luận của CNN sang **CPU Burst** (`_preferCpuInference = true`). Thời gian nhận diện giảm xuống chỉ còn **0.8 ms** (nhanh gấp 7 lần, không làm khựng màn hình).

---

## ✅ KẾT QUẢ KIỂM THỬ SAU KHI TÍCH HỢP

1. **Python Unit Tests:**
   ```powershell
   cd Tools; ..\.venv\Scripts\python -m unittest test_stream_protocol test_hand_identity test_pinch_state test_cpu_affinity
   ```
   ➜ **Ran 21 tests in 0.004s — OK (21/21 PASS)**.
2. **Unity Editor Validation Menu:**
   - `Tools/MagicTouch/Validate Tracking Stream` ➜ **PASS** (`[TrackingStream] PASS: pinchPending protocol, coalesced release, stale-packet timeout`).
   - `Tools/MagicTouch/Validate Drawing Repair` ➜ **PASS**.
3. **Độ an toàn của Scene & Git:**
   - Scene `SampleScene.unity` sạch sẽ 100%, không bị sửa đổi ngoài ý muốn.
   - Nhánh `main` gốc được bảo toàn nguyên vẹn.

---

## 📌 HƯỚNG DẪN DÀNH CHO NGƯỜI DÙNG

### 1. Cách Test game trong Unity ngay bây giờ:
- Bấm nút **Play ▶️** trên thanh công cụ Unity.
- **Nếu test bằng Chuột:**
  - Chọn menu `Tools > Magic Touch > Switch Input > Mouse Simulation (PC Debug)`.
  - Bấm nút **PLAY SOLO** trên Game View ➜ Nhấn giữ chuột trái và rê vẽ ký hiệu (V, ^, —, |, O, Z, W, S, E, X) để nổ bóng và đánh Boss.
- **Nếu test bằng AR Webcam:**
  - Chọn menu `Tools > Magic Touch > Switch Input > AR Hand Tracking (MediaPipe)`.
  - Bấm **PLAY SOLO** ➜ Đưa tay trước webcam và chụm ngón tay để vẽ (tốc độ tracking đạt 30 FPS, nét vẽ mượt mà không bị đứt).

### 2. Cách trở về nhánh làm việc ban đầu bất cứ lúc nào:
Mở PowerShell tại thư mục dự án và chạy:
- **Quay về nhánh làm dở (kèm toàn bộ tài nguyên Animation Pháp sư của bạn):**
  ```powershell
  git checkout backup-my-work-before-test
  ```
- **Quay về nhánh `main` gốc ban đầu:**
  ```powershell
  git checkout main
  ```
