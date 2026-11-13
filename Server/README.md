# Magic Touch – Server bảng xếp hạng (Docker + Cloud)

> Mới bắt đầu, chưa biết Docker/Cloud? Đọc **[HUONG_DAN.md](HUONG_DAN.md)** (từng bước, kèm bảng tiến độ).

Server nhỏ (FastAPI + PostgreSQL) chạy trong Docker, đặt trên cloud:

- **Đăng ký / đăng nhập** (mật khẩu băm PBKDF2, token JWT 30 ngày)
- **Nhận điểm** từ game sau mỗi ván Solo (tài khoản, hoặc khách chỉ nhập tên)
- **Bảng xếp hạng** (điểm cao nhất mỗi người chơi) + trang web tại `/`

```text
Game Unity (PC, webcam) ──HTTPS/JSON──► Docker container (FastAPI) ──► PostgreSQL
                                             └─► trang web: đăng ký, đăng nhập, bảng xếp hạng
```

Game AR vẫn chạy trên máy người chơi (cần webcam + Python tracker), **chỉ server được đóng gói Docker**.

## 1. Chạy thử trên máy

Cần Docker Desktop.

```bash
cd Server
docker compose up --build -d
```

- Trang web: http://localhost:8000
- Tài liệu API (thử trực tiếp): http://localhost:8000/docs
- Tắt: `docker compose down` (thêm `-v` để xóa luôn dữ liệu)

Muốn đổi mật khẩu/khóa: copy `.env.example` thành `.env` trong `Server/` (không commit).

## 2. API

| Method | Đường dẫn | Mô tả |
|---|---|---|
| POST | `/api/auth/register` | `{username, password, display_name?}` → `{token, username, display_name}` |
| POST | `/api/auth/login` | `{username, password}` → `{token, …}` |
| GET | `/api/me` | (Bearer token) thông tin + điểm cao nhất |
| GET | `/api/me/scores` | (Bearer token) các ván gần đây |
| POST | `/api/scores` | `{score, mode: "solo"\|"versus", player_name?}`; có token thì lấy tên tài khoản, không có thì bắt buộc `player_name` |
| GET | `/api/leaderboard?mode=solo&limit=20` | bảng xếp hạng |
| GET | `/health` | kiểm tra sống |

Khách không được dùng tên trùng tên hiển thị của tài khoản đã đăng ký.

## 3. Đưa lên cloud

Image chỉ cần 3 biến môi trường:

| Biến | Ý nghĩa |
|---|---|
| `DATABASE_URL` | URL PostgreSQL (`postgres://…` cũng được). Bỏ trống = SQLite trong container (**mất dữ liệu khi redeploy**, chỉ để thử) |
| `JWT_SECRET` | chuỗi bí mật dài, ngẫu nhiên, **giữ cố định** |
| `GAME_API_KEY` | (tùy chọn) khóa game phải gửi kèm khi nộp điểm |

Cổng: container đọc biến `PORT` (mặc định 8000), các nền tảng cloud tự đặt.

### Cách A: Render (miễn phí, dễ nhất)
1. Đẩy repo lên GitHub.
2. Render → **New → PostgreSQL** (free) → copy *Internal Database URL*.
3. Render → **New → Web Service** → chọn repo → *Language*: **Docker**, *Root Directory*: `Server`.
4. Environment: `DATABASE_URL` = URL ở bước 2, `JWT_SECRET` = chuỗi ngẫu nhiên.
5. Deploy → nhận URL dạng `https://magictouch-xxxx.onrender.com`.

(Gói free sẽ "ngủ" sau 15 phút không dùng, request đầu tiên mất khoảng 30-50 giây.)

### Cách B: Google Cloud Run / Azure / AWS (đẩy image)
```bash
cd Server
docker build -t magictouch-leaderboard .
docker tag magictouch-leaderboard <registry>/magictouch-leaderboard:1.0
docker push <registry>/magictouch-leaderboard:1.0
```
Rồi tạo service từ image, đặt 3 biến môi trường ở trên. Database: Cloud SQL / Neon / Supabase (PostgreSQL).

### Cách C: VPS có Docker
Copy thư mục `Server/` lên, tạo `.env`, chạy `docker compose up -d --build`, mở cổng 8000 (nên đặt sau Nginx/Caddy có HTTPS).

## 4. Nối game Unity với server

Thêm vào file `.env` ở thư mục gốc dự án (cạnh `Assets/`):

```env
LEADERBOARD_URL=https://magictouch-xxxx.onrender.com
GAME_API_KEY=            # trùng với server nếu có đặt
ONLINE_LEADERBOARD=1     # 0 = tắt khung online
```

Bản build (.exe) không có `.env` thì dùng `LeaderboardApi.DefaultServerUrl`
(`Assets/Scripts/Online/LeaderboardApi.cs`), nhớ sửa thành URL cloud trước khi build.

Trong game:
- **Menu chính**: khung góc phải trên: *Khách* (nhập tên) / *Đăng nhập* / *Đăng ký*, kèm Top 10.
- **Game Over (Solo)**: tự gửi điểm và hiện hạng; nếu chưa có tên thì hiện ô nhập tên + nút *Lưu điểm*.
- Chế độ Versus không gửi điểm (2 người chung 1 máy).

## 5. Giới hạn hiện tại
- Điểm do client gửi nên có thể bị giả mạo; `GAME_API_KEY` chỉ chặn được gửi bừa (khóa nằm trong game).
- Chưa có giới hạn tần suất (rate limit), chưa có quên mật khẩu.
- Gửi điểm thất bại (mất mạng) thì có nút *Thử gửi lại* ở màn Game Over; thoát màn đó là mất điểm ván đó.
- Khung trong game dùng IMGUI (tạo lúc chạy, không sửa scene). Muốn giao diện đẹp hơn, đồng bộ với UI hiện có thì cần làm panel trong `SampleScene`.
