# Hiệu năng camera, tracking và nhận diện ký tự (phân tích 2026-10-30)

Báo cáo này ghi lại nguyên nhân làm camera giật, tracking mất tay, FPS giảm và nhận diện kém khi chạy nhánh của PhungVu, các thay đổi đã làm, số đo trước/sau, cách kiểm tra lại và cách khôi phục.

## 1. Nhánh và mốc so sánh

| Tên | Commit | Ghi chú |
|---|---|---|
| `test-phungvu` (local, nhánh đang mở khi bắt đầu) | `686fc0d` | Trùng `origin/PhungVu`. Tên đúng trên remote là **`PhungVu`**. Thư mục `branch_phungvu/` là một bản clone riêng của cùng nhánh này |
| `origin/main` = `origin/QuangDai_Feature` | `a08505d` | = `686fc0d` + 1 commit `.gitignore`. Nhánh chính trên remote **đã chứa toàn bộ thay đổi của PhungVu** |
| `main` (local) | `c953ec1` | Đã cũ, chưa cập nhật từ remote |
| Mốc "trước PhungVu" | `a97b5d9` | Đỉnh `QuangDai_Feature`/`feature/portable-setup` trước khi gộp PhungVu; về code gần như trùng `HoangAnh_Feature` (`6894579`) |
| `fix/tracking-performance` | tạo từ `686fc0d` | Nhánh chứa các sửa đổi trong báo cáo này |

Vì `origin/main` đã gồm PhungVu, phép so sánh có ý nghĩa là `a97b5d9..686fc0d` (3 commit `bfb8db5`, `80f98d4`, `686fc0d`).

### Thay đổi của PhungVu liên quan tới tracking và nhận diện

| Commit | Thay đổi | Ảnh hưởng tìm thấy |
|---|---|---|
| `bfb8db5` fixMediaPipelineneural | Chụm tay do streamer quyết định theo tỉ lệ khoảng cách ngón cái–trỏ / cỡ lòng bàn tay (`pinch_state.py`, 0.18/0.25); thêm `pinchVersion`, `releaseCount`, `pinchRatio` vào gói UDP. Unity đặt lại bộ lọc chụm theo gói mỗi lần nhận; `DrawingManager` buộc nhấc bút mỗi khi `releaseCount` tăng. Scene: `pinchEnter/Exit` 0.05/0.06 (chỉ còn là dự phòng), `multiStrokeDelay` 0.5 (code vẫn kẹp tối đa 0.45 s) | Ý tưởng tốt (không phụ thuộc khoảng cách tới camera, không mất nhấc bút khi Unity gộp gói) nhưng **mất cơ chế chống nhiễu 1 mẫu** mà README và `InteractionRepairChecks` vẫn mô tả: một khung nhiễu làm đứt nét |
| `80f98d4` MinorErrorFixed | Viết lại `HasThreeArms` (cổng cấu trúc chữ E), áp `HasRequiredStructure` trong `SentisRecognizer` và khi chốt; mức Easy sinh quái đều 2,5 s; cảnh/cửa sổ test CNN | Không đụng camera, MediaPipe, UDP |
| `686fc0d` VeryBigUpDate… | Cổng vòng kín cho chữ O; màu phản hồi theo kết quả hạ quái; model ONNX tinh chỉnh chữ `^` (cùng đồ thị, cùng kích thước); script huấn luyện | Không đụng camera, MediaPipe, UDP |

**Kết luận so sánh:** PhungVu không sửa luồng camera, cấu hình MediaPipe, luồng UDP hay video. Phần "FPS giảm mạnh / mất tay" có nguyên nhân chính ở môi trường chạy (mục 2.1), còn thay đổi chụm tay của PhungVu làm nặng thêm phần đứt nét (mục 2.3).

## 2. Nguyên nhân

### 2.1 MediaPipe bị xếp lên E-core của CPU lai (nguyên nhân chính của FPS thấp)

Máy kiểm tra: Intel i7-12700H (6 P-core + 8 E-core, 20 luồng), Windows 11, webcam "HD Webcam", nguồn điện cắm, power plan High performance.

- `Logs/Editor.log` của phiên Unity ngày 23/10 trên `test-phungvu` (141 cửa sổ 2 s): camera trung vị 16,0 FPS (6,8–30,9), **tracking trung vị 4,1 FPS (1,4–13,4; 138/141 cửa sổ dưới 10 FPS), inference trung vị 242 ms/khung (72–588)**. `Logs/tracking-performance.json` cùng phiên (23/10 16:07): tracking 8,38 FPS, inference 116,65 ms. Mốc cũ trong `fps-validation.md`: tracking 28,75 FPS, inference 29,4 ms.
- Tái hiện **không cần Unity** (Python chạy console, không có tay trong hình): inference trung vị 74 ms, trung bình 100 ms → không phải do code Unity.
- Tắt EcoQoS/power throttling: không cải thiện → không phải do tiết kiệm điện.
- Thử cùng model trên khung ảnh tổng hợp 640×480, không dùng camera, xen kẽ các lượt:

| Lịch CPU | Trung vị | Trung bình | p90 |
|---|---:|---:|---:|
| Windows tự xếp (5 lượt) | 54–69 ms | 61–112 ms | 93–261 ms |
| Chỉ E-core | 67 ms | 91 ms | 174 ms |
| **Chỉ P-core (3 lượt)** | **19,1–20,3 ms** | **19,6–20,4 ms** | **21,7–23,5 ms** |

MediaPipe chia mỗi khung cho nhiều luồng và phải chờ luồng chậm nhất. Streamer chạy ẩn (Unity khởi chạy bằng `pythonw`, không cửa sổ) bị Windows coi là tiến trình nền và đẩy sang E-core, nhất là khi Unity Editor và các tiến trình khác đang chiếm P-core. Luồng đọc camera cũng bị đói CPU, nên FPS camera rơi theo (4–27 FPS).

### 2.2 Chuỗi hệ quả trong Unity khi tracking chậm

1. `HandTrackingBridge.Update` coi mọi tay là mất nếu 0,25 s không có gói. Ở 1,4–7 FPS gói cách nhau 140–700 ms nên tay "mất" ngay giữa hai gói, bộ lọc vị trí bị reset (giật).
2. `DrawingManager` giữ nét 0,14 s khi mất tracking (`trackingLossGrace`), quá hạn thì **hủy cả nét đang vẽ**.
3. Cử chỉ dưới 6 điểm (`_minGesturePoints`) bị bỏ qua như một cú chạm. Một ký hiệu vẽ trong 0,8–1,6 s ở 4 FPS chỉ còn 2–5 điểm: trên bộ benchmark tổng hợp, **97,4 % cử chỉ ở 4 FPS không tới được bộ nhận diện** (mục 4.4).

### 2.3 Mất chống nhiễu khi nhả chụm (từ `bfb8db5`)

`PinchState` nhả ngay ở khung đầu tiên có tỉ lệ > 0.25; Unity chép nguyên trạng thái đó (`transition.Reset`) và nhấc bút theo `releaseCount`. Một khung nhiễu giữa nét làm nét bị cắt: mất 2 mẫu và phần còn lại thành nét mới (StrokeID mới). Trên benchmark (mục 4.4), một khung nhiễu ở 22 FPS hạ tỉ lệ nhận đúng từ 91,0 % xuống **76,8 %**; ở 8 FPS từ 67,2 % xuống 21,2 %.

### 2.4 Camera/video

Video gửi bằng luồng riêng (JPEG chất lượng 50, tối đa 30 FPS), không chờ tracking. Khi CPU bị chiếm như mục 2.1, luồng đọc camera bị chậm nên hình nền giật. DSHOW vẫn được dùng trước vì mở nhanh; webcam này đạt 30 FPS khi đủ sáng, chỉ ~10 FPS khi phòng tối (driver tự kéo dài thời gian phơi sáng), đây là giới hạn phần cứng/ánh sáng. Video và landmark không cùng khung (video có thể đi trước con trỏ một khoảng bằng thời gian inference); khi inference còn 20–70 ms thì độ lệch nhỏ, nên chưa đổi.

### 2.5 Độ trễ nhận diện

`SampleScene` lưu `SentisRecognizer._backendType = 256`, trong Inference Engine 2.6.1 đó là **GPUCompute** (log: `Worker initialized successfully using backend: GPUCompute`). Mỗi lần nhấc bút, `DrawingManager.PrepareGestureCompletion` gọi nhận diện và đọc kết quả GPU **đồng bộ trên main thread**. Đo trong Editor (240 cử chỉ thật của benchmark): GPUCompute trung bình 5,40 ms, p95 15,0 ms; CPU trung bình 0,78 ms, p95 2,02 ms; hai backend cho cùng ký hiệu, sai khác xác suất lớn nhất 4,2e-7.

## 3. Thay đổi đã làm

| File | Thay đổi |
|---|---|
| `Tools/cpu_affinity.py` (mới) | Đọc `GetSystemCpuSetInformation`, chọn các luồng có `EfficiencyClass` cao nhất và đặt affinity cho tiến trình streamer. CPU đồng nhất, nhiều processor group hoặc không phải Windows: giữ nguyên |
| `Tools/test_cpu_affinity.py` (mới) | 5 test: chọn mask cho CPU lai, bỏ qua CPU đồng nhất/nhiều group, đọc bản ghi Windows |
| `Tools/mediapipe_streamer.py` | Gọi `apply_cpu_policy` trước khi tạo camera/MediaPipe; tham số `--cpu-policy auto\|default` (mặc định `auto`); in dòng `CPU Scheduling`, ghi `cpu_policy` vào `Logs/tracking-performance.json`; gói landmark có thêm trường tùy chọn `pinchPending` |
| `Tools/pinch_state.py` | Khôi phục quy tắc của `PinchTransitionFilter`: 1 mẫu mở chưa rõ (0.25–0.375) chỉ tạm dừng nét (`pending`), mẫu mở kế tiếp mới nhả; mở rõ (≥ 0.375) nhả ngay. Giữ nguyên tỉ lệ theo cỡ lòng bàn tay và bộ đếm `releases` của PhungVu |
| `Tools/test_pinch_state.py` | Cập nhật 3 test theo quy tắc trên (mở nhẹ thì tạm dừng rồi nhả; nhả thật vẫn được đếm khi chụm lại; tỉ lệ không đổi theo kích thước/khung hình) và thêm 2 test (mở rõ nhả ngay; 1 khung nhiễu không cắt nét) |
| `Assets/Scripts/Tracking/PinchTransitionFilter.cs` | Thêm `SetStreamState(pinching, releasePending)` |
| `Assets/Scripts/Tracking/HandTrackingBridge.cs` | Đọc `pinchPending` (thiếu thì là `false`) và chuyển vào `IsPinchReleasePending` để dừng mực; thời hạn mất gói `StalePacketTimeout` = 3 × nhịp gói đo được, kẹp 0,25–0,75 s (ở 30 FPS vẫn là 0,25 s như cũ) |
| `Assets/Scripts/Tracking/MediaPipeUDPReceiver.cs` | Chuyển tiếp `pinchPending` ở đường nhận landmark dự phòng |
| `Assets/Scripts/Recognition/SentisRecognizer.cs` | Trường mới `_preferCpuInference` (mặc định bật): CNN 28×28 chạy CPU bất kể `Backend Type` trong scene |
| `Assets/Editor/TrackingStreamChecks.cs` (+ `.meta`, mới) | Menu `Tools/MagicTouch/Validate Tracking Stream` và `RunBatch` cho batchmode: kiểm tra giao thức `pinchPending`, thời hạn mất gói, CNN mặc định chạy CPU; đo CPU/GPU; chấm benchmark nhận diện; ghi `Logs/tracking-stream-checks.json`. Không mở hay lưu scene |
| `Tools/benchmark_stream.py` (mới) | Chạy streamer thật với session-id, nhận UDP như Unity và đo gói/s, khoảng cách gói, số lần sẽ bị coi là mất tay (quy tắc 0,25 s cũ và quy tắc thích ứng), kiểm tra định dạng gói và video. Không lưu ảnh |
| `Train/air_stroke_benchmark.py` (mới) | Sinh bộ cử chỉ "vẽ trong không khí" từ `stroke_dataset.py`: lấy mẫu theo FPS tracking, rung 1,5 px, bỏ mẫu chụm đầu tiên và điểm cách nhau ≤ 2 px như `DrawingManager`, tùy chọn 1 khung nhiễu (`split` = hành vi không chống nhiễu, `freeze` = có `pinchPending`) |
| `README.md`, `AGENTS.md` | Cập nhật mô tả chụm tay, pipeline, lỗi thường gặp, lệnh kiểm tra |

Không sửa: scene/prefab (`SampleScene.unity` giữ nguyên), model ONNX, package, phiên bản Python/Unity, code khởi chạy Python (`StartPythonStreamerStealth`, `.bat`), EnemySpawner, physics, UI, chế độ chơi.

**Tương thích:** giao thức chỉ thêm trường tùy chọn, `pinchVersion` vẫn là 1. Unity cũ + streamer mới: bỏ qua `pinchPending`, vẫn chạy (vẽ thêm đúng 1 mẫu khi đang chờ xác nhận nhả). Unity mới + streamer cũ: `pinchPending = false`, hành vi như PhungVu.

## 4. Kết quả đo

Điều kiện chung: cùng máy ở mục 2.1. Trong lúc đo có các tiến trình Python không liên quan của người dùng chạy nền (một job xử lý ~5 lõi buổi sáng; cài torch và xử lý OCR buổi chiều), nên số tuyệt đối có phần bi quan. Các lượt A/B được chạy xen kẽ để so sánh công bằng.

### 4.1 Streamer thật, có tay trong hình, có gửi video (headless, 4 lượt xen kẽ, mỗi lượt ~26 s, số trong bảng là khoảng của các cửa sổ 2 s)

| Lượt | Lịch CPU | Camera FPS | Tracking FPS | Inference |
|---|---|---:|---:|---:|
| 1 | mặc định | 16,3–27,0 | 3,4–11,2 | 88–295 ms |
| 2 | **P-core** | **30,1–31,0** | **16,1–26,0** | **36–61 ms** |
| 3 | mặc định | 4,0–23,6 | 0,3–3,2 | 286–2965 ms |
| 4 | **P-core** | **30,2–30,8** | **13,9–22,4** | **44–71 ms** |

### 4.2 Đầu-cuối qua UDP, nhận gói như Unity (cùng cách đo với `Tools/benchmark_stream.py`, 25 s mỗi lượt, lúc máy tải nhẹ hơn)

| Lượt | Lịch CPU | Gói landmark/s | Khoảng cách gói p50 / p90 / max | Số lần > 250 ms | Gói có tay | Video FPS |
|---|---|---:|---|---:|---:|---:|
| default-1 | mặc định | 15,25 | 62,6 / 80,6 / 111,4 ms | 0 | 377/381 | 21,3 |
| auto-1 | P-core | 17,52 | 61,7 / 68,0 / 75,2 ms | 0 | 438/438 | 21,3 |
| default-2 | mặc định | 12,86 | 77,3 / 94,7 / 298,6 ms | 1 | 318/318 | 21,5 |
| auto-2 | P-core | 27,95 | 32,1 / 47,1 / 1765,7 ms | 3 | 73/696 | 21,6 |

Khi máy tải nhẹ, chênh lệch nhỏ hơn (12,9–15,3 so với 17,5–28,0 gói/s); khi máy bận (mục 4.1, log Unity 23/10) lịch mặc định sụp xuống 0,3–11 FPS còn P-core giữ 14–26 FPS. Lượt auto-2 có một quãng ngừng 1,77 s lúc hầu như không có tay trong hình, nguyên nhân chưa xác định (lượt này khung có tay chỉ 73/696). Mọi gói đều có đủ `pinch`, `pinchPending`, `pinchVersion`, `releaseCount`, `landmarks`; timestamp tăng dần; 100 % gói video mang đúng tiền tố `MTAR` + session + JPEG (~18,5 KB).

### 4.3 Unity 6000.5.6f1 batchmode

| Lần chạy | Biên dịch | Kiểm tra |
|---|---|---|
| Trước khi sửa (`InteractionRepairChecks.Run`) | 0 lỗi | PASS, jitter ratio 0,020 |
| Sau khi sửa (`TrackingStreamChecks.RunBatch`) | 0 lỗi; chỉ có cảnh báo CS0618 cũ ở `DrawingRepairChecks.cs:48` (in 2 lần) | TrackingStream PASS, InteractionRepair PASS (jitter 0,020, như cũ), DrawingRepair PASS; thoát sạch (`Batchmode quit successfully`) |

Lần chạy sau sửa đầu tiên (gọi `EditorApplication.Exit`) báo segfault **sau khi** cả 3 kiểm tra đã PASS, lúc Unity buộc tắt `AssetImportWorkerHW0`. Đã đổi sang `-quit` như lần chạy mốc; chạy lại thì thoát sạch.

### 4.4 Benchmark nhận diện (`Train/air_stroke_benchmark.py`, model ONNX thật, cổng cấu trúc E/O và quy tắc chốt sớm của game)

550 hình gốc (50/lớp × 11 lớp) dùng chung cho mọi điều kiện; tỉ lệ tính trên 500 ký hiệu thật, riêng lớp Unknown (50 mẫu) tính tỉ lệ bị nhận nhầm.

| Điều kiện | Nhận đúng | Nhận sai | Bị từ chối | Trong đó < 6 điểm | Unknown bị nhận |
|---|---:|---:|---:|---:|---:|
| 4 FPS | 2,4 % | 0 % | 97,6 % | 97,4 % | 0 % |
| 8 FPS | 67,2 % | 0,6 % | 32,2 % | 4,8 % | 2 % |
| 15 FPS | 82,2 % | 0 % | 17,8 % | 0 % | 2 % |
| 22 FPS | 91,0 % | 0 % | 9,0 % | 0 % | 0 % |
| 30 FPS | 96,0 % | 0 % | 4,0 % | 0 % | 2 % |
| 8 FPS + 1 khung nhiễu, không chống nhiễu | 21,2 % | 1,0 % | 77,8 % | 30,2 % | 4 % |
| 8 FPS + 1 khung nhiễu, có `pinchPending` | 54,8 % | 0,2 % | 45,0 % | 19,2 % | 2 % |
| 22 FPS + 1 khung nhiễu, không chống nhiễu | 76,8 % | 0,4 % | 22,8 % | 0 % | 0 % |
| 22 FPS + 1 khung nhiễu, có `pinchPending` | **91,2 %** | 0 % | 8,8 % | 0 % | 0 % |

Không có trường hợp nào bị chốt sớm thành ký hiệu khác (0 % ở mọi điều kiện). Đây là dữ liệu tổng hợp, không thay cho thử với người chơi thật; nó cho thấy độ tin cậy nhận diện phụ thuộc chủ yếu vào FPS tracking và việc giữ nét liền, không phải model.

### 4.5 Unit test Python

`..\.venv\Scripts\python -m unittest test_stream_protocol test_hand_identity test_pinch_state test_cpu_affinity` (thư mục `Tools/`): 21 test OK (trước khi sửa: 14 test OK).

## 5. Cách chạy và kiểm tra lại

1. Bấm Play như bình thường (chế độ AR). Console có dòng `[HandTrackingBridge] Launched Python Streamer…`. Sau vài giây, `Logs/tracking-performance.json` có `"cpu_policy": "performance cores only (affinity mask 0x…)"` cùng `tracking_fps`, `inference_ms`; Inspector của `HandTrackingBridge` › *Measured Camera Performance* hiện cùng số đo.
2. So sánh với lịch cũ khi chạy tay (đã tắt Auto Launch theo README): `python Tools\mediapipe_streamer.py --stream-video --preview --cpu-policy default`.
   Đo luồng như Unity nhận (Unity phải đang tắt vì cùng cổng 9050/9051), mỗi lượt ghi `Logs/stream-benchmark-<policy>.json`:
   ```bat
   .venv\Scripts\python.exe Tools\benchmark_stream.py --cpu-policy auto --seconds 25
   .venv\Scripts\python.exe Tools\benchmark_stream.py --cpu-policy default --seconds 25
   ```
3. Kiểm tra trong Editor (an toàn, không mở/lưu scene): `Tools > MagicTouch > Validate Tracking Stream`, hoặc khi Unity đang tắt:
   ```bat
   "C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe" -batchmode -quit -projectPath "<gốc dự án>" -executeMethod TrackingStreamChecks.RunBatch -logFile Logs\checks.log
   ```
4. Benchmark nhận diện: `.venv\Scripts\python.exe Train\air_stroke_benchmark.py --output Logs\air-strokes.json`, rồi chạy lệnh ở bước 3 (mặc định đọc `Logs/air-strokes.json`, hoặc thêm `-airStrokes <json>`). Kết quả trong `Logs/tracking-stream-checks.json`.
5. Muốn quay lại GPU cho CNN: bỏ chọn *Prefer Cpu Inference* trên `SentisRecognizer` (thay đổi scene, nên lưu có chủ ý).

Số đo thô của lần phân tích này nằm trong `Logs/perf-20261030/` (log Unity, đầu ra streamer, JSON đầu-cuối, benchmark); thư mục `Logs/` bị `.gitignore`.

## 6. Giới hạn kiểm thử

- **Chưa chạy Play Mode có webcam trong Unity.** Phần Unity được kiểm bằng biên dịch batchmode và các kiểm tra Edit Mode. Phần streamer được đo trực tiếp với webcam thật nhưng không có Unity chạy song song. Cần thử trên máy có webcam: Play, vẽ V, ^, O, Z, W, S, E, X và chế độ Versus 2 tay.
- Trong các lượt đo có người trước camera nhưng không có thao tác vẽ có chủ đích; độ chính xác nhận diện chỉ đo trên dữ liệu tổng hợp.
- FPS game loop của Unity chưa được đo lại; chưa đo độ trễ từ chuyển động tay tới nét vẽ.
- Chỉ đo trên một máy (i7-12700H). Trên CPU không lai (AMD, Intel đời cũ) chính sách P-core không đổi gì.
- `Validate UI Bindings` không chạy vì cần mở `SampleScene`; các thay đổi không đụng UI.
- Các tiến trình nền không liên quan làm số đo dao động; các lượt A/B đã chạy xen kẽ để giảm sai lệch.

## 7. Khôi phục

- Nhánh gốc `test-phungvu` không bị sửa; nhánh sao lưu `backup/test-phungvu-pre-perf-20261030` trỏ `686fc0d`.
- Bản sao lưu ngoài Git: `_backup/20261030-pre-perf/` (bị loại trừ qua `.git/info/exclude`) gồm `MagicTouchAR-all-refs.bundle` (mọi nhánh, `git bundle verify` OK), `worktree-source-snapshot.tar` (Assets/Scripts, Editor, Models, Scenes, Tools, Train, Packages, ProjectSettings, README/AGENTS/CLAUDE, `.env`) và `git-state.txt`.
- Tắt riêng từng phần không cần sửa code: `--cpu-policy default` (chạy tay), bỏ chọn *Prefer Cpu Inference*.
