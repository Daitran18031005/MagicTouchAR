# SPECIFICATION: HAND-DRAWN PIXEL ART CHARACTER DESIGN (DUAL SPEC)

> **Mã tài liệu:** SPEC-ART-PIXEL-DUAL  
> **Phạm vi áp dụng:** Boss ($512 \times 512\text{ px}$) và Elite/Large Enemy ($512 \times 512\text{ px}$)  
> **Mục tiêu:** Khóa chặt phong cách arcade cổ điển 16-bit (Neo-Geo, CPS3), loại bỏ triệt để hiện tượng AI noise, color banding và biến dạng tỷ lệ trong game engine.

---

## 1. Canvas & Scale Hierarchy (Phân cấp Khung hình & Tỷ lệ)

| Thuộc tính | Boss Units | Elite / Large Enemy Units |
| :--- | :--- | :--- |
| **Kích thước tiêu chuẩn** | $512 \times 512\text{ px}$ mỗi frame | $512 \times 512\text{ px}$ mỗi frame |
| **Quy chuẩn tỷ lệ** | Boss khổng lồ (Monumental scale) | Quái tinh anh / Boss phụ (Heavy minion scale) |
| **Điểm neo (Pivot)** | `Bottom-Center` hoặc `Center` | `Bottom-Center` |
| **Tỷ lệ hiển thị lưới** | Đồng nhất $1:1$ pixel ratio | Đồng nhất $1:1$ pixel ratio |

### Nguyên tắc bảo toàn lưới (Grid Integrity):
* **Tuyệt đối không có Mixels:** Không trộn lẫn các kích thước điểm ảnh khác nhau trên cùng một nhân vật hoặc giữa các nhân vật tương tác với nhau.
* **Không làm mượt dưới pixel (No Sub-pixel Smoothing):** Nghiêm cấm các kỹ thuật nội suy làm mềm hạt viền, brush digital làm mờ (anti-aliasing mờ) hoặc bộ lọc hậu kỳ (post-render blur).

---

## 2. Color & Palette Management (Quản lý Bảng màu & Đổ bóng)

### Giới hạn Palette (Indexed Color):
* **Boss Units:** Giới hạn nghiêm ngặt từ $24$ đến $32$ màu tổng cộng.
  * Mỗi cụm vật liệu (da/vảy, sừng/xương, dung nham/lửa) tối đa $4 - 6$ sắc độ (shades per ramp).
* **Enemy Units:** Giới hạn nghiêm ngặt từ $16$ đến $24$ màu tổng cộng.
  * Mỗi cụm vật liệu tối đa $3 - 5$ sắc độ.

### Nguyên tắc đổi sắc thái (Hue-Shifting & Shading Logic):
* **Cel-shading thủ công:** Phân định rõ ranh giới các khối bóng đổ, ưu tiên xếp cụm hạt (pixel clustering).
* **Đổi tông màu động (Dynamic Hue-Shifting):**
  * Vùng đón sáng (Highlights): Chuyển dần về tông ấm ($golden\ yellow \to peach \to amber$).
  * Vùng khuất sáng (Shadows): Chuyển dần về tông lạnh sâu ($obsidian\ purple \to deep\ navy \to dark\ teal$).
* **Nghiêm cấm:**
  * Không sử dụng gradient xám đục (muddy grayscale gradients).
  * Không dùng kỹ thuật dither hạt ngẫu nhiên do AI sinh ra (no procedural / airbrush dithering).

---

## 3. Linework & Contour Rules (Quy chuẩn Đường nét & Viền)

```
[Outside Canvas Alpha: 0%] 
       │
[Outer Silhouette Contour: 100% Solid Dark]
       │
[Selective Outline (Sel-out): Tone-matched 1-2px]
       │
[Internal Core Clusters & Highlights]
```

### Độ dày nét (Line Weight):
* **Boss ($512 \times 512\text{ px}$):** Sử dụng viền chọn lọc (selective outline - sel-out) dày $1 - 2\text{ px}$ đồng điệu với màu vật liệu cục bộ, bao bọc bên ngoài bởi đường viền tối màu vững chắc nhằm tách biệt nhân vật khỏi phông nền.
* **Enemy ($512 \times 512\text{ px}$):** Giữ nét viền sắc gọn chuẩn $1\text{ px}$ sel-out, không làm dày viền gây thô cứng chi tiết.

### Quy chuẩn kênh Alpha (Transparency & Cleanup):
* **Độ trong suốt tuyệt đối (Hard Alpha):** Giá trị alpha chỉ chấp nhận $0$ (trong suốt) hoặc $255$ (rắn), không chấp nhận giá trị bán trong suốt ($0 < \alpha < 255$).
* **Khử nhiễu biên (Edge Cleanup):**
  * Triệt tiêu $100\%$ các hạt lẻ loi (zero orphan/stray pixels).
  * Loại bỏ hoàn toàn viền hào quang mờ (zero alpha fringes).

---

## 4. Anatomy, Silhouette & Visual Hierarchy (Hình thể & Khối)

* **Khả năng nhận diện tức thì (High Readability):**
  * Hình khối cơ bắp rắn rỏi, gân guốc, các khớp nối và giáp vảy được cách điệu gãy gọn.
  * Tối ưu góc nhìn từ người chơi: Nếu góc nhìn từ dưới lên hoặc nhìn xuống người chơi ($3/4$ hoặc $4/4$ frontal top-down), đầu và ánh mắt phải chúc thẳng về đối tượng chiến đấu.
* **Tỷ lệ bộ phận chủ đạo (Feature Scaling):**
  * **Boss:** Sở hữu các đặc điểm ngoại cỡ mang tính áp đảo (ví dụ: sải cánh to hơn cơ thể $1.3\times$, vũ khí hoặc bộ vuốt khổng lồ chiếm trọn $2/3$ nửa trên màn hình).
  * **Enemy:** Giữ dáng gọn gàng, tính cơ động cao, hỗ trợ làm nổi bật Boss chính mà không gây rối mắt cho người chơi.

---

## 5. Animation & Sprite Sheet Matrix (Tiêu chuẩn Đóng gói Sheet)

Để tránh hiện tượng Unity nén xẹp texture khi dải ngang vượt quá $8192\text{ px}$, mọi sprite sheet phải tuân thủ dạng ma trận lưới:

### Ma trận cho Boss ($512 \times 512\text{ px}$):
* **Bố cục đề xuất:** $8\text{ cột} \times 6\text{ hàng}$ (tối đa 48 ô).
* **Kích thước tổng:** $4096 \times 3072\text{ px}$ (phù hợp tối ưu với chuẩn `Max Size: 4096` hoặc `8192` trên Unity).
* **Trật tự nạp frame:** Cắt tuần tự từ trái qua phải, từ trên xuống dưới khớp theo mảng JSON metadata.

### Ma trận cho Enemy ($512 \times 512\text{ px}$):
* **Bố cục đề xuất:** $8\text{ cột} \times 4\text{ hàng}$ (tối đa 32 ô).
* **Kích thước tổng:** $2048 \times 1024\text{ px}$ (nhẹ nhàng, tiết kiệm VRAM).

---

## 6. Unity Engine Import Checklist

Trước khi đưa vào Animator hoặc Script nạp frame, bắt buộc kiểm tra các thông số sau trong Inspector:

- [ ] **Texture Type:** `Sprite (2D and UI)`
- [ ] **Sprite Mode:** `Multiple`
- [ ] **Sprite Editor Slice:** `Grid By Cell Size` (Nhập đúng $512 \times 512$ hoặc $512 \times 512$)
- [ ] **Filter Mode:** `Point (no filter)` (Ngăn mờ viền)
- [ ] **Max Size:** `4096` (đối với Enemy/Boss dạng lưới) hoặc `8192` (nếu vượt quá 4096)
- [ ] **Compression:** `None` (Ngăn nhiễu hạt DXT5/BC3)