# Hướng dẫn từ A đến Z: Docker + Cloud + Đăng ký/Đăng nhập + Bảng xếp hạng

> Viết cho người **chưa biết gì** về Docker, server hay cloud. Đọc theo thứ tự từ trên xuống.
> Mỗi bước có: **làm gì**, **gõ gì**, **thấy gì là đúng**, **sai thì sửa thế nào**.

---

## Mục lục
- [Phần 0. Bạn muốn gì, và đã làm được đến đâu](#phan-0)
- [Phần 1. Hiểu 5 khái niệm cơ bản (5 phút đọc)](#phan-1)
- [Phần 2. Bản đồ: file nào làm việc gì](#phan-2)
- [Phần 3. Chạy server trên máy của bạn bằng Docker](#phan-3)
- [Phần 4. Thử trong game Unity](#phan-4)
- [Phần 5. Đưa server lên Cloud (Render, miễn phí)](#phan-5)
- [Phần 6. Nối game với server trên Cloud](#phan-6)
- [Phần 7. Build game ra file .exe cho người khác chơi](#phan-7)
- [Phần 8. Lưu lại bằng Git (commit)](#phan-8)
- [Phần 9. Gặp lỗi thì làm sao](#phan-9)
- [Phần 10. Câu hỏi hay gặp (để trả lời khi thuyết trình)](#phan-10)

---

<a id="phan-0"></a>
## Phần 0. Bạn muốn gì, và đã làm được đến đâu

### Yêu cầu của bạn
1. Đóng gói **Docker**.
2. Người chơi vào game, **nhập tên**, chơi xong thì **điểm được gửi lên Cloud**.
3. Trên Cloud có **bảng thành tích (bảng xếp hạng)**.
4. Có thêm **Đăng ký + Đăng nhập** ngay trên Cloud.

### Bảng tiến độ

| # | Hạng mục | Trạng thái | Ghi chú |
|---|---|---|---|
| 1 | Viết server (chương trình chạy trên Cloud) | ✅ Xong | `Server/app/main.py` |
| 2 | Cơ sở dữ liệu lưu tài khoản + điểm | ✅ Xong | PostgreSQL, 2 bảng `users`, `scores` |
| 3 | Đăng ký / Đăng nhập (API) | ✅ Xong, đã test | Mật khẩu được mã hóa, không lưu dạng chữ thường |
| 4 | Nhận điểm: có tài khoản **hoặc** khách chỉ nhập tên | ✅ Xong, đã test | |
| 5 | Bảng xếp hạng (API) | ✅ Xong, đã test | Mỗi người lấy điểm cao nhất |
| 6 | Trang web: Đăng ký, Đăng nhập, Bảng xếp hạng | ✅ Xong, đã xem trên trình duyệt | Mở địa chỉ server là thấy |
| 7 | Đóng gói Docker (`Dockerfile`, `docker-compose.yml`) | ✅ Xong, đã chạy được trên máy | |
| 8 | Code Unity: khung nhập tên / đăng nhập / đăng ký ở menu | ✅ Viết xong, compile 0 lỗi | ⚠️ **Chưa bấm Play thử trong Unity** |
| 9 | Code Unity: hết ván Solo tự gửi điểm + hiện hạng | ✅ Viết xong, compile 0 lỗi | ⚠️ **Chưa bấm Play thử trong Unity** |
| 10 | **Đưa server lên Cloud thật** | ❌ Chưa làm | Cần tài khoản của **bạn** (GitHub + Render). Làm theo [Phần 5](#phan-5) |
| 11 | Nối game với server trên Cloud | ❌ Chưa làm | Làm sau bước 10, theo [Phần 6](#phan-6) |
| 12 | Lưu vào Git (commit) | ❌ Chưa làm | Theo [Phần 8](#phan-8) |

**Tóm lại:** phần **code đã xong khoảng 100%**, phần **chạy trên máy đã kiểm tra**.
Còn lại là các việc **chỉ bạn làm được** vì cần đăng nhập tài khoản của bạn: đưa lên Cloud, rồi điền địa chỉ Cloud vào game.

### Những gì cố tình KHÔNG làm (và vì sao)
- **Không cho cả game vào Docker**: game cần webcam, card đồ họa và cửa sổ hiển thị; Docker không làm được những việc này. Thứ được đóng gói Docker là **server lưu điểm**. Đây cũng là cách các game thật làm (game chạy trên máy người chơi, server chạy trên cloud).
- **Không sửa scene `SampleScene.unity`**: khung đăng nhập trong game được tạo bằng code lúc chạy, để không làm hỏng scene của nhóm.
- **Chế độ Versus không gửi điểm**: 2 người chơi chung 1 máy nên không biết điểm thuộc tài khoản nào.

---

<a id="phan-1"></a>
## Phần 1. Hiểu 5 khái niệm cơ bản

| Từ | Hiểu đơn giản | Trong dự án này |
|---|---|---|
| **Server** | Một chương trình chạy liên tục, ngồi chờ người khác gửi yêu cầu tới rồi trả lời | Chương trình Python trong `Server/app/main.py` |
| **Database (cơ sở dữ liệu)** | Cái "sổ ghi chép" có bảng, hàng, cột (giống Excel) | PostgreSQL, có bảng `users` (tài khoản) và `scores` (điểm) |
| **API** | Danh sách các "câu hỏi" server hiểu được, ví dụ "đăng nhập", "cho tôi bảng xếp hạng" | `/api/auth/login`, `/api/leaderboard`, … |
| **Docker** | Cái "hộp" chứa sẵn server + mọi thứ nó cần (Python, thư viện). Máy nào có Docker là chạy được, không phải cài từng thứ | `Server/Dockerfile` là "công thức" đóng hộp |
| **Cloud** | Máy tính của công ty khác (Render, Google…) bật 24/7, có địa chỉ Internet. Bạn gửi cái hộp Docker lên đó để ai cũng truy cập được | Phần 5 hướng dẫn đưa lên Render |

### Dữ liệu đi như thế nào

```text
 ┌──────────────────────┐   1. "Tôi tên Lan, được 2000 điểm"   ┌──────────────────────────┐
 │  Game Unity          │ ───────────────────────────────────► │  CLOUD (Render)          │
 │  (máy người chơi,    │                                      │  ┌────────────────────┐  │
 │   có webcam)         │ ◄─────────────────────────────────── │  │ Hộp Docker: Server │  │
 └──────────────────────┘   2. "Đã lưu, bạn đứng hạng #1"      │  └─────────┬──────────┘  │
                                                               │            │ ghi/đọc     │
 ┌──────────────────────┐   3. Mở web xem bảng xếp hạng        │  ┌─────────▼──────────┐  │
 │  Trình duyệt web     │ ───────────────────────────────────► │  │ Database Postgres  │  │
 │  (điện thoại, PC)    │   Đăng ký / Đăng nhập trên web       │  └────────────────────┘  │
 └──────────────────────┘                                      └──────────────────────────┘
```

### "Token" là gì?
Khi đăng nhập đúng, server đưa cho game một **token** (một chuỗi ký tự dài, giống "vé vào cửa").
Mỗi lần gửi điểm, game đưa kèm vé này để server biết là ai. Vé có hạn 30 ngày; hết hạn thì đăng nhập lại.
Game tự cất vé, bạn không cần làm gì.

---

<a id="phan-2"></a>
## Phần 2. Bản đồ: file nào làm việc gì

```text
MagicTouchAR-main/
├── Server/                         ← TOÀN BỘ phần Cloud (mới thêm)
│   ├── app/
│   │   ├── main.py                 ← code server: đăng ký, đăng nhập, lưu điểm, bảng xếp hạng
│   │   └── static/index.html       ← trang web (giao diện đăng ký/đăng nhập/bảng xếp hạng)
│   ├── Dockerfile                  ← "công thức" đóng server vào hộp Docker
│   ├── docker-compose.yml          ← chạy 2 hộp cùng lúc trên máy bạn: server + database
│   ├── requirements.txt            ← danh sách thư viện Python server cần
│   ├── .env.example                ← mẫu file cấu hình bí mật (mật khẩu DB, khóa…)
│   ├── README.md                   ← tài liệu kỹ thuật ngắn
│   └── HUONG_DAN.md                ← file bạn đang đọc
│
├── Assets/Scripts/Online/          ← phần trong game Unity (mới thêm)
│   ├── LeaderboardApi.cs           ← game "nói chuyện" với server (gửi điểm, đăng nhập…)
│   └── OnlineAccountOverlay.cs     ← khung trên màn hình: nhập tên, đăng nhập, đăng ký, Top 10
│
└── .env.example                    ← thêm 3 dòng: LEADERBOARD_URL, GAME_API_KEY, ONLINE_LEADERBOARD
```

Không có file cũ nào của game bị sửa (trừ `.env.example` được thêm 3 dòng ở cuối).

---

<a id="phan-3"></a>
## Phần 3. Chạy server trên máy của bạn bằng Docker

Mục đích: thử mọi thứ trên máy mình trước, chắc chắn chạy rồi mới đưa lên Cloud.

### Bước 3.1 – Mở Docker Desktop
- Máy bạn **đã cài sẵn** Docker Desktop.
- Bấm phím Windows, gõ **Docker Desktop**, mở lên. Chờ đến khi góc dưới bên trái hiện chữ **Engine running** (màu xanh), khoảng 30 giây – 1 phút.

> Lỗi hay gặp: nếu mở Docker mà báo cần **WSL**, làm theo nút "Update/Install" mà Docker hiện ra, rồi khởi động lại máy.

### Bước 3.2 – Mở cửa sổ lệnh tại thư mục `Server`
1. Mở File Explorer, vào thư mục `D:\Hoc Tap\hk7\Công nghệ mới\MagicTouchAR-main\Server`.
2. Bấm vào thanh địa chỉ ở trên cùng, xóa hết, gõ `powershell` rồi Enter. Một cửa sổ chữ xanh/đen hiện ra, đã đứng sẵn ở thư mục `Server`.

### Bước 3.3 – Bật server
Gõ lệnh sau rồi Enter:

```bash
docker compose up --build -d
```

- Lần đầu mất **2–5 phút** (tải Python, PostgreSQL về).
- **Đúng** khi cuối cùng thấy các dòng có chữ `Started` / `Healthy`.

Ý nghĩa: `up` = bật, `--build` = đóng hộp từ code mới nhất, `-d` = chạy ngầm (đóng cửa sổ vẫn chạy).

### Bước 3.4 – Kiểm tra
Mở trình duyệt (Chrome, Edge) vào các địa chỉ:

| Địa chỉ | Thấy gì là đúng |
|---|---|
| http://localhost:8000/health | Chữ `{"status":"ok"}` |
| http://localhost:8000 | Trang **Magic Touch Leaderboard** có ô Đăng nhập/Đăng ký và bảng xếp hạng |
| http://localhost:8000/docs | Trang tài liệu API, bấm từng mục → **Try it out** để thử |

Thử: bấm tab **Đăng ký**, tạo tài khoản (tên đăng nhập không dấu, mật khẩu ≥ 6 ký tự) → thấy hiện tên mình và "0 ván đã chơi".

### Bước 3.5 – Các lệnh hay dùng (gõ trong thư mục `Server`)

| Muốn | Lệnh |
|---|---|
| Xem server có đang chạy không | `docker compose ps` |
| Xem nhật ký (log) khi có lỗi | `docker compose logs api` |
| Tắt server (giữ dữ liệu) | `docker compose down` |
| Tắt và **xóa sạch** dữ liệu | `docker compose down -v` |
| Sửa code xong, chạy lại | `docker compose up --build -d` |

---

<a id="phan-4"></a>
## Phần 4. Thử trong game Unity

Server ở Phần 3 phải **đang chạy**.

1. Mở dự án trong Unity (hoặc nếu đang mở, **bấm vào cửa sổ Unity** để nó nạp 2 file code mới; chờ vòng xoay ở góc dưới bên phải chạy xong).
2. Xem tab **Console**: không được có lỗi đỏ **mới**. (Các lỗi cũ đã biết: cổng 8080/8090, `npm` — không liên quan.)
3. Bấm **Play**.
4. Ở **menu chính**, góc **trên bên phải** có khung mới:
   - Tab **Khách**: gõ tên → chơi, điểm lưu dưới tên này.
   - Tab **Đăng nhập**: dùng tài khoản đã tạo trên web ở bước 3.4.
   - Tab **Đăng ký**: tạo tài khoản ngay trong game.
   - Bên dưới: **Top 10 (Solo)**. Nếu hiện "(chưa kết nối được server)" → server chưa bật.
5. Chọn chơi **Solo**, chơi đến khi **Game Over** (hoặc thắng boss).
6. Khung góc phải hiện: `Đã lưu … điểm · Hạng #… · Kỷ lục …`.
   - Nếu chưa nhập tên và chưa đăng nhập → hiện ô nhập tên + nút **Lưu điểm**.
7. Mở http://localhost:8000 → thấy tên và điểm trong bảng xếp hạng.

Muốn **tắt** khung online (ví dụ khi demo không có mạng): thêm dòng `ONLINE_LEADERBOARD=0` vào file `.env` ở thư mục gốc dự án.

> ⚠️ Bước này mình (Claude) **chưa thử** được vì cần bạn bấm Play và chơi. Nếu có lỗi, chụp Console gửi lại.

---

<a id="phan-5"></a>
## Phần 5. Đưa server lên Cloud (Render, miễn phí)

Chọn **Render** vì: miễn phí, không cần thẻ ngân hàng cho gói free, tự đọc `Dockerfile`, có sẵn PostgreSQL.

### Bước 5.1 – Code phải nằm trên GitHub
Render lấy code từ GitHub.
- Repo hiện tại: `github.com/Daitran18031005/MagicTouchAR` (của bạn **Dai**).
- Bạn cần: hoặc được Dai thêm làm thành viên (**Settings → Collaborators**), hoặc **Fork** repo về tài khoản của bạn (nút **Fork** góc trên phải trang GitHub).
- Commit và đẩy (push) thư mục `Server/` lên nhánh đang dùng (xem [Phần 8](#phan-8)). Render chỉ thấy code **đã push**.

### Bước 5.2 – Tạo tài khoản Render
Vào https://render.com → **Get Started** → đăng nhập bằng **GitHub** (cho Render quyền đọc repo).

### Bước 5.3 – Tạo database PostgreSQL
1. Trên trang Render bấm **New +** → **Postgres**.
2. Điền:
   - **Name**: `magictouch-db`
   - **Region**: **Singapore** (gần Việt Nam nhất)
   - **Plan**: **Free**
3. Bấm **Create Database**, chờ trạng thái **Available**.
4. Kéo xuống phần **Connections**, copy dòng **Internal Database URL** (bắt đầu bằng `postgresql://…`). Dán tạm vào Notepad.

> Lưu ý: database **free** của Render có thể **hết hạn sau một thời gian** (xem thông báo trên trang Render). Đủ cho đồ án/demo. Muốn lâu dài miễn phí có thể dùng **Neon** (neon.tech) hoặc **Supabase**, chỉ cần lấy URL PostgreSQL của họ dán vào `DATABASE_URL`.

### Bước 5.4 – Tạo Web Service từ Docker
1. **New +** → **Web Service** → chọn repo MagicTouchAR.
2. Điền:
   - **Name**: `magictouch-leaderboard` (tên này thành một phần địa chỉ web)
   - **Language / Runtime**: **Docker**
   - **Branch**: nhánh bạn đã push (ví dụ `QuangDai_Feature`)
   - **Region**: **Singapore** (phải **cùng** region với database)
   - **Root Directory**: `Server` ← **quan trọng**, sai chỗ này là lỗi
   - **Instance Type**: **Free**
3. Mục **Environment Variables**, bấm **Add** để thêm:

   | Key | Value |
   |---|---|
   | `DATABASE_URL` | dán URL copy ở bước 5.3 |
   | `JWT_SECRET` | một chuỗi dài bất kỳ, tự gõ bừa ≥ 32 ký tự, ví dụ `mT9x...` (**giữ bí mật, không đổi về sau**) |
   | `GAME_API_KEY` | (tùy chọn) một chuỗi bí mật thứ hai; bỏ trống cũng được |

4. Bấm **Create Web Service** (hoặc **Deploy**). Chờ 3–7 phút, khi log hiện `Uvicorn running` và trạng thái **Live** là xong.
5. Trên cùng có địa chỉ dạng **`https://magictouch-leaderboard.onrender.com`**. Đây là **địa chỉ Cloud** của bạn.

### Bước 5.5 – Kiểm tra trên Cloud
- Mở `https://<địa-chỉ-của-bạn>.onrender.com/health` → `{"status":"ok"}`.
- Mở `https://<địa-chỉ-của-bạn>.onrender.com` → trang đăng ký/đăng nhập/bảng xếp hạng. **Gửi link này cho bạn bè là họ xem được.**

> Gói free sẽ **"ngủ"** nếu 15 phút không ai dùng. Lần mở đầu tiên sau đó mất **30–60 giây** để "thức dậy". Trước khi demo, mở trang web trước 1 phút cho nó thức.

### Bước 5.6 – Khi sửa code server
Chỉ cần commit + push lên GitHub, Render **tự build và deploy lại**. Dữ liệu trong database vẫn giữ nguyên.

---

<a id="phan-6"></a>
## Phần 6. Nối game với server trên Cloud

### Khi chạy trong Unity Editor
1. Ở thư mục gốc dự án (cạnh thư mục `Assets`), mở file **`.env`** (nếu chưa có thì copy `.env.example` thành `.env`).
2. Thêm/sửa dòng:
   ```env
   LEADERBOARD_URL=https://magictouch-leaderboard.onrender.com
   GAME_API_KEY=
   ```
   (Nếu ở Render bạn có đặt `GAME_API_KEY` thì điền **giống hệt** vào đây.)
3. Bấm Play lại. Top 10 trong game giờ lấy từ Cloud.

> File `.env` **không được đưa lên Git** (đã có sẵn trong `.gitignore`), vì chứa thông tin riêng của máy.

### Kiểm tra
Chơi 1 ván Solo → mở trang web trên Cloud → thấy điểm. Xong yêu cầu của bạn. 🎉

---

<a id="phan-7"></a>
## Phần 7. Build game ra file .exe cho người khác chơi

File `.env` không đi kèm bản build, nên phải **ghi cứng** địa chỉ Cloud vào code:

1. Mở `Assets/Scripts/Online/LeaderboardApi.cs`.
2. Tìm dòng:
   ```csharp
   public const string DefaultServerUrl = "http://localhost:8000";
   ```
3. Sửa thành địa chỉ Cloud của bạn:
   ```csharp
   public const string DefaultServerUrl = "https://magictouch-leaderboard.onrender.com";
   ```
4. Build như bình thường (**File → Build Profiles → Build**).

(Nếu bạn đặt `GAME_API_KEY` trên server thì có thể đặt file `.env` cạnh file `.exe` chứa dòng `GAME_API_KEY=...`.)

---

<a id="phan-8"></a>
## Phần 8. Lưu lại bằng Git (commit)

Gõ trong PowerShell tại thư mục **gốc dự án** (`MagicTouchAR-main`):

```bash
git status
```
Thấy `Server/`, `Assets/Scripts/Online/`, `.env.example` là đúng. **Không** được thấy `.env` hay `Server/data/`.

```bash
git add Server Assets/Scripts/Online .env.example
```

```bash
git commit -m "feat(online): Docker leaderboard server with register/login and Unity score upload"
```

```bash
git push origin QuangDai_Feature
```

Lưu ý:
- Sau khi Unity nạp code, nó tạo thêm file `.meta` trong `Assets/Scripts/Online/` (ví dụ `LeaderboardApi.cs.meta`). **Phải commit kèm** các file này (lệnh `git add` ở trên đã gồm cả thư mục).
- Theo luật nhóm (AGENTS.md): **không push thẳng vào `main`**; tạo Pull Request nếu muốn gộp vào `main`.

---

<a id="phan-9"></a>
## Phần 9. Gặp lỗi thì làm sao

| Hiện tượng | Nguyên nhân | Cách sửa |
|---|---|---|
| `failed to connect to the docker API` | Docker Desktop chưa mở | Mở Docker Desktop, chờ *Engine running* |
| `port is already allocated` / `8000 already in use` | Đã có server khác chạy cổng 8000 | `docker ps` xem cái nào, `docker stop <tên>`; hoặc sửa `"8000:8000"` thành `"8001:8000"` trong `docker-compose.yml` |
| Trong game: "(chưa kết nối được server)" | Server chưa chạy / sai `LEADERBOARD_URL` | Mở `<địa chỉ>/health` trên trình duyệt; kiểm tra `.env` |
| Trong game: "Không kết nối được server" lúc gửi điểm (Cloud) | Server free đang "ngủ" | Chờ 1 phút rồi bấm **Thử gửi lại** |
| "Tên này thuộc một tài khoản đã đăng ký" | Khách dùng tên trùng tên tài khoản | Đổi tên khác hoặc đăng nhập |
| "Sai game key" | `GAME_API_KEY` trong game khác trên server | Điền giống nhau ở cả 2 nơi |
| "Phiên đăng nhập hết hạn" | Token quá 30 ngày hoặc đã đổi `JWT_SECRET` | Đăng nhập lại; đừng đổi `JWT_SECRET` |
| Render báo `failed to read dockerfile` | Sai **Root Directory** | Settings → Root Directory = `Server` |
| Render deploy xong nhưng lỗi kết nối DB | Sai `DATABASE_URL` hoặc khác region | Copy lại *Internal Database URL*, cùng region Singapore |
| Mọi người bị đăng xuất sau khi deploy | Không đặt `JWT_SECRET` (server tự tạo ngẫu nhiên mỗi lần chạy) | Đặt `JWT_SECRET` cố định ở Render |

---

<a id="phan-10"></a>
## Phần 10. Câu hỏi hay gặp (dùng khi thuyết trình)

**Hỏi: Tại sao không cho cả game vào Docker?**
Docker chạy chương trình không có màn hình, không có webcam và card đồ họa. Game AR cần cả 3. Kiến trúc chuẩn là: client (game) chạy trên máy người chơi, server chạy trong Docker trên cloud.

**Hỏi: Mật khẩu có bị lộ không?**
Không lưu mật khẩu gốc. Server lưu bản **băm PBKDF2-SHA256** (200.000 vòng, có salt ngẫu nhiên). Kể cả người xem được database cũng không đọc ra mật khẩu. Trên Cloud dùng **HTTPS** nên đường truyền được mã hóa.

**Hỏi: Bảng xếp hạng tính thế nào?**
Mỗi tên người chơi lấy **điểm cao nhất** của mình, xếp giảm dần; bằng điểm thì ai đạt trước đứng trên. Có cột số ván đã chơi; người chơi khách có nhãn "khách".

**Hỏi: Công nghệ dùng gì?**
- Server: **Python 3.11 + FastAPI** (web framework), **SQLAlchemy** (làm việc với database), **PyJWT** (token đăng nhập), **Uvicorn** (chạy server).
- Database: **PostgreSQL 16** (trên máy chạy bằng Docker; trên Cloud dùng Render Postgres).
- Đóng gói: **Docker** + **Docker Compose**.
- Cloud: **Render** (Web Service kiểu Docker).
- Game: Unity gọi API bằng **UnityWebRequest**, dữ liệu dạng **JSON**.

**Hỏi: Có gian lận điểm được không?**
Có thể, vì điểm do game gửi lên (đây là giới hạn chung của game chấm điểm phía client). Đã có: giới hạn điểm tối đa, kiểm tra tên, khóa `GAME_API_KEY` để chặn gửi bừa. Muốn chống gian lận thật cần server tự kiểm tra cả ván chơi, nằm ngoài phạm vi đồ án.

**Hỏi: Danh sách API?**

| Việc | Gọi |
|---|---|
| Đăng ký | `POST /api/auth/register` |
| Đăng nhập | `POST /api/auth/login` |
| Thông tin của tôi | `GET /api/me` |
| Các ván gần đây của tôi | `GET /api/me/scores` |
| Gửi điểm | `POST /api/scores` |
| Bảng xếp hạng | `GET /api/leaderboard?mode=solo` |
| Kiểm tra server sống | `GET /health` |

Xem và thử trực tiếp ở `<địa chỉ server>/docs`.

---

### Việc còn có thể làm thêm (nếu muốn điểm cao hơn)
- Làm khung đăng nhập trong game bằng UI đẹp, đồng bộ với menu hiện có (cần sửa scene, hỏi nhóm trước).
- Chức năng quên mật khẩu, đổi tên hiển thị.
- Giới hạn số lần gửi điểm mỗi phút (chống spam).
- Lưu tạm điểm khi mất mạng và tự gửi lại lần sau.
