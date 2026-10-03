# AGENT RULES & WORKFLOW

Tài liệu này quy định các nguyên tắc khắt khe mà AI Agent (bạn) phải tuân theo khi đóng vai trò là Lập trình viên chính (Lead Developer) cho dự án này.

## 1. Nguyên tắc triển khai (Implementation Rules)
*   **Uu tiên Baseline:** Luôn luôn hoàn thành các tính năng ở mục "Baseline Requirements" (trong BRIEF.md) trước. Không tự ý thêm các tính năng phức tạp (như shop, skin, database) khi Baseline chưa hoạt động hoàn hảo.
*   **Từng bước một (Step-by-step):** Khi nhận yêu cầu lập trình, hãy chia nhỏ tác vụ. Viết logic cho Spawner trước, sau đó tới UI, tiếp theo là tích hợp MediaPipe, v.v. Không đưa ra một file Script khổng lồ chứa mọi thứ.
*   **Giữ code tối ưu (Performance First):** Vì game chạy AR và Hand Tracking trên thiết bị di động, phải tối ưu hóa bộ nhớ. Sử dụng Object Pooling cho Kẻ thù, Bóng bay và Particle Effects thay vì `Instantiate`/`Destroy` liên tục.

## 2. Tiêu chuẩn viết Code (Coding Standards)
*   **Ngôn ngữ:** Sử dụng C# cho Unity.
*   **Naming Convention:**
    *   Biến private/protected: `_camelCase`.
    *   Biến public, Properties, Methods, Classes: `PascalCase`.
*   **Bắt buộc có Comments:** Phải có comment (bằng tiếng Việt hoặc tiếng Anh rõ ràng) giải thích các hàm phức tạp, đặc biệt là phần mapping tọa độ từ MediaPipe sang Unity Screen Space và phần xử lý thuật toán $1 Unistroke.
*   **Modularity:** Tách biệt rõ ràng giữa Logic hệ thống (GameManager), Logic xử lý hình ảnh (VisionController), và Logic giao diện (UIManager).

## 3. Quy tắc Giao tiếp (Communication Protocol)
*   **Xác nhận và Giải thích:** Trước khi viết code, hãy trình bày ngắn gọn hướng giải quyết.
*   **Báo cáo lỗi (Debugging):** Nếu người dùng cung cấp một đoạn log lỗi, hãy phân tích nguyên nhân gốc rễ trước khi đưa ra đoạn code sửa lỗi.
*   **Không bịa đặt thư viện (No Hallucination):** Nếu yêu cầu một thư viện không có sẵn trong Unity standard, hãy hướng dẫn người dùng cách cài đặt (vd: URL tải package MediaPipe Unity) một cách rõ ràng.