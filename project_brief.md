# PROJECT BRIEF: AR MAGIC TOUCH (Tên dự án tạm thời)

## 1. Tổng quan dự án (Project Overview)
Dự án là một tựa game Mobile AR (Augmented Reality) lấy cảm hứng từ game "Magic Touch: Wizard for Hire". Thay vì vuốt trên màn hình cảm ứng, người chơi sẽ sử dụng camera trước của thiết bị để tracking chuyển động của ngón tay trỏ. Bằng cách "vẽ" các ký hiệu trong không khí, người chơi sẽ phá huỷ các quả bóng bay mang ký hiệu tương ứng để ngăn chặn kẻ thù rơi xuống tháp phòng thủ.

## 2. Mục tiêu cốt lõi (Baseline Requirements)
Agent có nhiệm vụ hoàn thành mức Baseline bao gồm:
*   Giao diện game cơ bản (UI).
*   Kết nối và hiển thị luồng video từ camera trước làm nền trò chơi.
*   Tích hợp hệ thống Hand Tracking để nhận diện được ngón tay trỏ.
*   Hiển thị được nét vẽ từ quỹ đạo của ngón tay trỏ trên màn hình.
*   Nhận diện hình dạng vừa vẽ, so khớp với bóng bay của kẻ thù và phá huỷ chúng nếu khớp.

## 3. Cơ chế Game (Core Mechanics)
*   **Kẻ thù & Bóng bay:** Kẻ thù (Hiệp sĩ/Robot) xuất hiện từ cạnh trên màn hình, bị treo bởi 1 hoặc nhiều quả bóng bay. Trên mỗi bóng bay có khắc một ký hiệu (V, Z, ^, -, v.v.). Kẻ thù sẽ từ từ rơi xuống dưới.
*   **Thao tác người chơi (AR Tracking):** Camera trước liên tục theo dõi bàn tay. Khi người chơi chụm ngón cái và ngón giữa (Pinch) làm điều kiện kích hoạt, ngón trỏ sẽ trở thành "bút vẽ". Quỹ đạo di chuyển của ngón trỏ được ghi lại và hiển thị trên màn hình.
*   **Xử lý hình vẽ (Shape Recognition):** Khi thao tác vẽ kết thúc, thuật toán (khuyến nghị $1 Unistroke Recognizer) sẽ đọc quỹ đạo và phân loại hình vẽ.
*   **Va chạm & Phá huỷ:** Nếu hình vẽ khớp với ký hiệu trên một (hoặc nhiều) quả bóng bay, bóng bay đó sẽ nổ. Kẻ thù mất toàn bộ bóng sẽ rơi tự do và bị tiêu diệt, ghi điểm cho người chơi.
*   **Chế độ Dual (Multiplayer - 2 Người chơi):** Màn hình chia làm hai nửa trái/phải ngầm định. Hệ thống theo dõi 2 bàn tay đồng thời. Bàn tay xuất hiện ở nửa nào sẽ tính điểm cho người chơi ở nửa đó.

## 4. Tech Stack Đề xuất
*   **Engine:** Unity 3D (C#).
*   **Hand Tracking:** MediaPipe Unity Plugin (nhận diện Landmark 8 của bàn tay).
*   **Gesture Recognition:** $1 Unistroke Recognizer (C# implementation).