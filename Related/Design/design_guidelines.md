# DESIGN & UX/UI GUIDELINES

Tài liệu này định hướng phong cách hình ảnh và trải nghiệm người dùng, đảm bảo game vừa có tính thẩm mỹ giống "Magic Touch" vừa phù hợp với môi trường AR phức tạp.

## 1. Phong cách hình ảnh (Art Style)
*   **Chủ đạo:** 2D Pixel Art kết hợp Casual. Tươi sáng, ngộ nghĩnh và vui nhộn.
*   **Bối cảnh (Background):** Khác với game gốc, bối cảnh chính của game này là luồng video trực tiếp từ camera (Real-world feed). Do đó, các yếu tố UI và Game Object phải được thiết kế để nổi bật trên bất kỳ nền video nào.
*   **Xử lý độ tương phản:** TẤT CẢ các thành phần game (chữ số, bóng bay, quái vật, thành trì) BẮT BUỘC phải có **viền ngoài (Outline) màu đen, dày** hoặc hiệu ứng đổ bóng (Drop shadow) mạnh để không bị chìm vào nền camera thực tế.

## 2. Trải nghiệm người dùng (UX) cho AR
*   **Hiển thị nét vẽ (Line Trail):** Nét vẽ của ngón tay phải dày, có màu sắc phản quang (như Neon Blue hoặc Neon Pink) và có Particle lấp lánh đi kèm để người chơi dễ dàng theo dõi trên nền camera. Nét vẽ cần từ từ mờ đi (fade out) sau khi hoàn thành.
*   **Feedback trực quan:**
    *   *Khi đang vẽ:* Đầu ngón tay (điểm Landmark 8) hiển thị một đốm sáng nhỏ báo hiệu hệ thống đang tracking tốt.
    *   *Khi nhận diện sai:* Nét vẽ nhấp nháy đỏ rồi vỡ vụn.
    *   *Khi nhận diện đúng:* Bóng bay nổ với hiệu ứng Particle và âm thanh "Pop" rõ ràng.
*   **Bố cục Dual-mode (2 Người chơi):**
    *   Chia đôi màn hình bằng một đường ranh giới mờ ảo ở chính giữa.
    *   UI Điểm số của Player 1 đặt ở góc dưới cùng bên trái.
    *   UI Điểm số của Player 2 đặt ở góc dưới cùng bên phải.
    *   Các ký hiệu bóng bay sinh ra bên nửa nào nên ưu tiên hướng về khu vực của người chơi đó.

## 3. Bố cục màn hình hiển thị (UI Layout)
*   **Khu vực an toàn (Safe Zone):** Hạn chế đặt UI quan trọng (Nút Pause, Cài đặt) ở những vị trí tay người chơi thường xuyên huơ qua (trung tâm màn hình). Đưa chúng lên các góc cao.
*   **Giao diện Tòa tháp:** Đặt ở khu vực đáy màn hình (Bottom). Có thể làm mờ nhẹ (Opacity 80%) phần nền của tháp để vẫn lộ một phần camera thực tế, tăng cảm giác AR.