# Thông tin quái vật trong game

Số liệu lấy từ code và asset ở thời điểm viết (Solo). Nguồn: `EnemySpawner.cs`, `EnemyBalloon.cs`, `GameManager.cs`, `SoloBossEncounter.cs`, và các asset `EmberDrake.asset`, `DullahanGuard.asset`.

## 1. Quái thường (creep)

Quái thường là một **bóng bay mang ký hiệu** kèm một nhân vật treo bên dưới. Vẽ đúng ký hiệu để nổ.

| Mục | Giá trị |
| --- | --- |
| Ký hiệu | 10: `V`, `^` (Caret), `—` (HorizontalLine), `\|` (VerticalLine), `O`, `Z`, `W`, `S`, `E`, `X` |
| Máu | 1 lần nổ |
| Hình nhân vật treo | 4 sprite trong `enemySpritePool` (Bat, Knight, Robot, enemy_frog/enemy_knight) |
| Vị trí sinh | Ngang ngẫu nhiên từ x = -2,2 đến 2,2, ở y = 5,5 phía trên màn hình |
| Nhịp sinh | Bắt đầu 2,0 s, mỗi quái sinh ra giảm 0,02 s, tối thiểu 0,8 s |
| Tốc độ rơi | Bắt đầu 0,5, mỗi quái sinh ra tăng 0,015, tối đa 3,5 (đơn vị/giây) |
| Chạm đất | Mất 1 tim (người chơi có 3 tim mỗi lượt) |

Độ khó (chọn trong cài đặt, `EnemySpawner`):

| Độ khó | Nhịp sinh | Tốc độ rơi |
| --- | --- | --- |
| Easy | cố định 2,5 s | × 0,75 |
| Normal | như bảng trên | × 1 |
| Hard | × 0,65 | × 1,4 |

Điểm số (`GameManager.OnSymbolRecognized`): vẽ một ký hiệu nổ **tất cả** bóng cùng ký hiệu đang có trên màn hình. Nổ `n` bóng cùng lúc được `10 × n × (n+1) / 2` điểm (1 → 10, 2 → 30, 3 → 60, 4 → 100) và cộng `n` vào combo. Vẽ ký hiệu không có bóng nào thì combo về 0.

Trong Solo, quái thường xuất hiện theo **giai đoạn có hẹn giờ** (xem [SOLO_BOSS_ENCOUNTER.md](SOLO_BOSS_ENCOUNTER.md)): 1 phút 30 giây trước Dullahan, 1 phút trước Ember. Hết giờ thì dừng sinh quái và phải nổ hết quái còn trên màn hình mới gọi được boss.

## 2. Boss phụ: Dread General Dullahan Guard

Hiệp sĩ đen không đầu, đứng trên **bệ đá cao hơn đầu người chơi**.

| Mục | Giá trị |
| --- | --- |
| Asset | `Assets/Sprites/bosses/castle/dullahan_guard/DullahanGuard.asset` |
| Xuất hiện | Sau 1:30 quái thường và dọn sạch màn hình |
| Máu | **200**, một thanh đỏ duy nhất; 10 máu mỗi bóng của boss, tức 20 bóng |
| Kích thước | Sprite 512×512 ở scale **1,5**, đáy chân luôn cao hơn đầu người chơi |
| Bóng quanh boss | 5, đứng yên ở hai bên, đổi ký hiệu mỗi 6 giây |
| Nhịp skill | 3,5 s từ đòn này tới đòn kế (tính cả thời gian cảnh báo) |
| Cảnh báo | 2 s, vòng cảnh báo tại vị trí người chơi; nổ một bóng của boss trong lúc cảnh báo thì hủy skill (hồi chiêu hủy 4 s) |
| Sát thương | Mỗi skill trúng làm mất 1 tim |
| Hiệu ứng skill | Cao 1,5× người chơi |

| Skill | Clip boss | Hiệu ứng | Kiểu |
| --- | --- | --- | --- |
| Cross Slash | `attack_cross_slash` | `cross_slash_fx` (chữ thập lửa) ở người chơi | `Slash` |
| Helmet Toss | `attack_helmet_toss` | `helmet_flame_burst_fx` (vòng lửa) ở chân người chơi | `GroundSpike` |

Cảnh xuất hiện: bệ đá nâng từ sàn lên trong khoảng 3 giây theo mức thanh máu lấp đầy, Dullahan nhảy từ trên cao xuống, tiếp đất làm bệ rung, khói bụi và đá vụn nổ lên (khói to 1,3× Dullahan, chỉ nằm trong bề rộng bệ). Hạ xong: lưu checkpoint.

## 3. Boss chính: Ember Drake Tyrant

Rồng lửa bay lơ lửng phía trên người chơi.

| Mục | Giá trị |
| --- | --- |
| Asset | `Assets/Sprites/bosses/castle/ember_drake_tyrant/EmberDrake.asset` |
| Xuất hiện | Sau khi hạ Dullahan, trụ 1:00 quái thường và dọn sạch màn hình |
| Máu | **300** = pha đỏ 100 + pha xanh 200; 10 máu mỗi bóng |
| Kích thước | Sprite 512×512 ở scale **1,5**, hover ở 70% chiều cao màn hình, luôn nằm trên đầu người chơi |
| Bóng quanh boss | 5, đổi ký hiệu mỗi 6 giây |
| Cảnh báo | 1,2 s; nổ bóng của boss trong lúc cảnh báo thì hủy skill (hồi chiêu hủy 4 s) |
| Hiệu ứng skill | Cao 1,5× người chơi |

| Pha | Clip nghỉ | Chu kỳ skill | Skill |
| --- | --- | --- | --- |
| Đỏ (100 máu) | `idle` | 3,5 s | Cào trái, cào phải, cào hai bên |
| Xanh (200 máu) | `idle_cracked` | 2,7 s | Cầu lửa (có 40% là cột lửa), gai đất, cào trái, cào phải, cào hai bên |

Cảnh xuất hiện: lốc xoáy lửa (1,4× boss) hiện tại chỗ boss đứng, hai cột lửa bùng lên sát mép trái và phải từ sàn lên trời, boss hiện dần trong lốc xoáy rồi gầm; lốc xoáy tan dần tại chỗ. Hạ xong: thắng game.
