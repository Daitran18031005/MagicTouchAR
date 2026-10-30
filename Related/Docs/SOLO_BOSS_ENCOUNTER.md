# Solo: lộ trình hai boss (Dullahan Guard rồi Ember Drake Tyrant)

Hệ thống chung: [BOSS_SYSTEM.md](BOSS_SYSTEM.md). Số liệu quái: [Monster_Infomations.md](Monster_Infomations.md). Dữ liệu từng boss nằm trong asset `DullahanGuard.asset` và `EmberDrake.asset`.

## Luồng trận (Solo)

| Bước | Nội dung |
| --- | --- |
| 1 | **Trụ 1:30** quái thường. Thanh tiến trình đầy theo thời gian thực. Nhạc `CombatCastleMusic.wav`. |
| 2 | Hết giờ: dừng sinh quái, **nổ hết quái còn trên màn hình** (chạm đất vẫn mất tim). |
| 3 | **Dullahan Guard** xuất hiện: thanh thành thanh máu đỏ duy nhất; bệ đá nâng lên trong ~3 giây theo mức thanh lấp đầy; Dullahan nhảy từ trên xuống, tiếp đất, khói bụi. Nhạc `boss_appear` rồi `boss_combat`. |
| 4 | Đánh Dullahan: 200 máu, 10 máu mỗi bóng. Hạ xong: lưu **checkpoint**, nhạc về nhạc quái. |
| 5 | **Trụ 1:00** quái thường (thanh theo thời gian), rồi nổ hết quái còn lại. |
| 6 | **Ember Drake Tyrant**. Hạ xong mới thắng game (VICTORY, `GameVictory`). |

**Lượt chơi**: tổng 5 lượt, mỗi lượt 3 tim. Chết ở bước 1–4: lượt mới chơi lại từ bước 1. Chết ở bước 5–6: lượt mới chơi lại từ bước 5. Hết lượt thứ 5: GAME OVER thật, chơi lại từ đầu. Về menu chính hoặc Restart trong Pause = chơi mới từ bước 1 với đủ 5 lượt. Thanh trên cùng hiển thị `ATTEMPT n/5`.

## Dullahan Guard (boss phụ)

- Art: `Assets/Sprites/bosses/castle/dullahan_guard/`, 512×512 mỗi frame, chân ở dòng 480 (pivot dưới giữa), scale 1,5.
- Một thanh máu đỏ 200; 5 bóng quanh boss đổi ký hiệu mỗi 6 giây.
- Hai skill chọn ngẫu nhiên không lặp liền nhau, mỗi đòn cách nhau 4,2 s (tính từ đòn trước), cảnh báo 2,4 s tại chỗ người chơi (xem "Vòng cảnh báo"); hiệu ứng cao 1,5× người chơi:
  - **Cross Slash** (`attack_cross_slash`): chỉ tay phải vung kiếm, tay trái giữ mũ; chém ngang rồi chém dọc, đứng yên; chữ thập lửa đánh vào người chơi. Impact frame 4.
  - **Helmet Toss** (`attack_helmet_toss`): ném mũ (mũ luôn hiện trên không), nổ vòng lửa dưới chân người chơi, bắt lại mũ. Impact frame 6.
- **Chặn skill**: nổ một bóng của boss lúc vòng còn xanh hoặc vàng thì hủy đòn; sang đỏ thì không hủy được. Skill trúng làm mất 1 tim.
- **Frame**: mỗi động tác có thêm một frame xen giữa mỗi cặp frame (idle 8, toss 13, cross slash 15, hurt 3, death 9, jump 7, land 7).

## Vòng cảnh báo (mọi boss)

Một vòng quanh người chơi (dưới chân với Helmet Toss), 2,4 s mỗi skill (`BossSkillDefinition._telegraphSeconds`):

| Pha | Thời gian | Màu | Hủy được |
| --- | --- | --- | --- |
| Nhắm | 0,9 s | Xanh | Có |
| Chuẩn bị | 1,1 s | Vàng | Có |
| Đánh | 0,4 s | Đỏ | Không |

Trong lúc vòng xanh và vàng, boss ở clip nghỉ (idle) rồi rung tăng dần; động tác chuẩn bị của skill (các frame trước `impact`) chạy đúng tốc độ gốc ngay trước `impact`, nên không bị kéo giãn thành trình chiếu ảnh (`BossVisual.PlaySkill`). Vòng thu nhỏ dần và nhấp nháy nhanh hơn ở mỗi pha; thân boss rung tăng dần khi gồng skill. Caster tự đếm giờ (không phụ thuộc clip), nên clip `hurt` không làm kẹt vòng; khi đang gồng hoặc tung skill, boss chỉ chớp đỏ, không phát `hurt`. Nhịp giữa hai đòn: Ember đỏ 4,2 s, Ember xanh 3,6 s, Dullahan 4,2 s. Không còn hồi chiêu sau khi hủy.
- **Cảnh xuất hiện** (`DullahanEntrance`): bệ đá nâng từ sàn khi thanh refill (3 s) → boss nhảy từ trên màn hình xuống (clip `jump`) → tiếp đất (`land`), bệ rung (`platform_landing`), khói bụi (`landing_dust`, to 1,3× Dullahan, chỉ nằm trong bề rộng bệ) và đá vụn (`stone_debris`) → boss về `idle`.
- Boss đứng trên bệ cao hơn đầu người chơi; bệ nằm sau người chơi.
- Bệ nâng lên có **rung màn hình** (xung rung nhẹ mỗi ~0,3 s, mạnh dần) kèm **đất đá vụn rơi từ bệ xuống sàn** và bụi bốc ở chân bệ, như kích hoạt một cạm bẫy cổ. Chân Dullahan đứng giữa mặt bệ (`_platformStand` 0,5), không sát mép sau.
- **Bị tiêu diệt**: clip `death` chạy trên bệ, rồi cả bệ **lún dần xuống nền** (~2,6 s, cùng rung màn hình và đá vụn như lúc nâng), xác tan dần theo bệ; xong mới sang giai đoạn quái kế (`IBossEntrance.Defeat`).
- Không dùng `SpriteMask`: mask làm bệ biến mất khi thoát menu rồi chơi lại. Phần bệ dưới mặt sàn và bụi ngoài bề rộng bệ được cắt thẳng trên sprite (`DullahanEntrance.Clip`, sprite cắt được cache theo từng hàng pixel).
- Hạ xong: clip `death`, chữ `DULLAHAN DEFEATED!   CHECKPOINT`, sau 2,2 s sang bước 5.

## Ember Drake Tyrant (boss chính)

Giữ nguyên cơ chế chiến đấu; khác với trước chỉ ở khâu chuẩn bị:

- Chờ boss: **trụ 1:00** (thanh đầy theo thời gian) thay cho "hạ 20 quái", rồi nổ hết quái còn lại.
- Cảnh vào (`BossEntrance`, đã lược bớt cho đỡ rối mắt): kén dung nham rơi xuống và **nằm sát sàn chỗ người chơi đứng**, nứt rồi nổ trong một đợt gió; boss bay lên khỏi kén tới chỗ lơ lửng, giữa hai cột lửa sát mép trái/phải (cách người chơi tối thiểu 1,2 lần bề ngang); boss gầm (vòng sóng, rung đất). Đã bỏ lốc xoáy, sét và gió mở đầu.
- **Bị tiêu diệt**: clip `death` chạy trong lúc boss **rơi từ chỗ lơ lửng xuống sàn người chơi đứng** (chạm sàn thì rung màn hình), xác nằm lại trên sàn (`BossDefinition.DeathFloorOffset`, `BossEntrance.Defeat`).
- Thanh sau cảnh vào được **refill màu đỏ** phủ lên lớp xanh (1,2 s), nhạc chuyển `boss_combat`, 5 bóng của boss xuất hiện.
- **Tổng 300 máu = đỏ 100 + xanh 200**, 10 máu mỗi bóng. Boss trúng đòn luôn chạy clip `hurt`.
  - **Pha đỏ**: nghỉ ở `idle`, chu kỳ 4,2 s, chỉ dùng cào trái, cào phải, cào hai bên.
  - **Pha xanh**: nghỉ ở `idle_cracked`, chu kỳ 3,6 s, dùng cả 5 skill (cầu lửa/cột lửa, gai đất, cào trái, cào phải, cào hai bên).
- Boss to 1,5×, sprite nguyên bản (không tách cánh), luôn nằm trên đầu người chơi. Cảnh báo 2,4 s (vòng 3 pha). Hiệu ứng skill cao 1,5× người chơi.
- Frame: mỗi động tác có thêm frame xen (idle 12, idle_cracked 12, hurt 5, death 15, skill_a 11, skill_b 11, cào trái và phải 11, slam down 17). Cào và slam kết thúc bằng động tác thu tay/móng rồi về thế bay. Thân nằm trong `ember_drake_tyrant_512_sheet.png`, các skill trong `ember_drake_tyrant_skills_sheet.png` (mỗi sheet tối đa 64 frame vì giới hạn 4096 px). Impact frame: skill_a, skill_b, slam là 6; cào trái và phải là 4.
- Hết xanh: clip `death`, hiện VICTORY. Thua phát `GameOver`.

## Mất tim

Mọi lần mất tim ở Solo (bóng chạm đất hoặc skill trúng) làm màn hình chớp đỏ và rung nhẹ (`Juice/ScreenHitFeedback`). Luật mất tim không đổi.

## Tệp chính

| Tệp | Nội dung |
| --- | --- |
| `Scripts/Gameplay/SoloBossEncounter.cs` | Vòng đời lộ trình: giai đoạn, thanh HUD, bóng quanh boss, lượt chơi, checkpoint |
| `Scripts/Gameplay/SoloBossProgress.cs` | Đồng hồ quái thường, phase `Clearing`, máu theo segment |
| `Scripts/Gameplay/Boss/*` | `BossDefinition`, `BossStage`, `BossSkillDefinition`, `BossAnimationSet`, `BossVisual`, `BossSkillCaster`, `BossSkillBag` |
| `Scripts/Juice/IBossEntrance.cs`, `BossEntrance.cs`, `DullahanEntrance.cs`, `BossFxSprite.cs`, `ScreenHitFeedback.cs` | Cảnh vào, sprite hiệu ứng, chớp/rung màn hình |
| `Scripts/Gameplay/MusicDirector.cs` | Nhạc menu / quái / boss |
| `Editor/SoloBossSetup.cs` | Menu `Tools > Magic Touch > Boss > Rebuild Ember Drake Wiring (manual)` và `Rebuild Dullahan Guard Wiring (manual)` |
| `Editor/BossAnimationImporter.cs` | Dựng `BossAnimationSet` từ manifest clip |
| `Editor/SoloBossChecks.cs` | Kiểm tra logic, định nghĩa, Play Mode, lượt chơi |

## Kiểm tra

`SoloBossChecks.ValidateModel()`, `ValidateDefinition()`, `ValidateRuntime()` (Play Mode, bỏ qua đồng hồ), `ValidateAttempts()` (Play Mode), `ValidateLifeLoss()` (Play Mode). Tự thử: Play, Solo, đợi hoặc trụ hết 1:30, nổ hết quái, xem bệ nâng và Dullahan nhảy xuống, phá bóng để đánh boss, rồi thử để chết để thấy TRY AGAIN và số lượt.

## Nhật ký hoàn thành

### Ember Drake Tyrant
1. Nối boss vào scene bằng menu dựng lại; sửa lỗi toán tử `??` trên đối tượng Unity khiến thiếu `SpriteRenderer`; sheet 4096×3584 phải import đủ cỡ.
2. Chỉnh theo nhận xét: nhạc quái dùng lại `CombatCastleMusic.wav` (bỏ crossfade bằng code vì làm hỏng âm thanh); nhịp skill 3,5 s đỏ / 2,7 s xanh, hủy skill có hồi chiêu 4 s; boss 1,5×, thử tách cánh bằng mask nhưng bị loạn sprite nên bỏ; lối vào mới (lốc xoáy, hai cột lửa, boss hiện trong lốc xoáy); skill cao 1,5× người chơi; sửa `NullReferenceException` khi skill trúng đòn mất tim cuối.
3. Dọn: frame rời của Ember và nhạc `creep_combat.ogg` chuyển vào `Related/_ToDelete`.

### Dullahan Guard
1. **Thiết kế**: lộ trình nhiều giai đoạn (`SoloBossProgress` đếm giờ + phase `Clearing`; `SoloBossEncounter` chạy chuỗi `_preludeBosses` rồi boss chính), 5 lượt chơi và checkpoint, `IBossEntrance` để mỗi boss có cảnh vào riêng, `DullahanEntrance` cho bệ đá. Dullahan: 200 máu, hai skill (Cross Slash, Helmet Toss), chu kỳ 3,5 s, cảnh báo 2 s.
2. **Art** (công cụ sprite, đường ảnh Codex CLI, không có đường video nên nhảy/tiếp đất làm bằng sheet frame): thêm 4 frame `jump`, 4 frame `land` bằng đúng bảng 24 màu của Dullahan (sheet mới `dullahan_guard_motion.png`, không đổi các sheet cũ); bệ đá, bụi, đá vụn theo brief và ảnh tham chiếu `platform_landing.png`, gộp thành `environment/dullahan_platform_master_spritesheet.png` (ô 640×256, bảng màu chung 24 màu, pivot dưới giữa) kèm `dullahan_platform.json`. Bệ idle là frame "trước va chạm" của sheet bệ rung vì cùng kích thước với các frame va chạm.
3. **Lỗi gặp khi test và cách sửa**:
   - Bệ và bụi là con của root boss nên bay theo boss khi nhảy: tách ra khỏi root.
   - Sheet Dullahan gốc có pivot ở chân, sheet nhảy/tiếp đất mới có pivot giữa, cộng thêm độ lệch chân: đặt pivot chân cho mọi sheet thân boss, `Feet Offset` = 0.
   - Bộ kiểm tra `ValidateAttempts` dừng sớm khi lượt 5 vừa bắt đầu: sửa vòng lặp, cho lượt 5 thua rồi mới kiểm tra GAME OVER.
4. **Kết quả kiểm tra** (Play Mode, tự động chụp ảnh): bệ nâng từ sàn theo thanh refill; boss nhảy xuống bệ, bụi to hơn boss và nằm trong bề rộng bệ; chân boss cao hơn đầu người chơi, đỉnh kiếm vẫn nằm trong màn hình; skill cao khoảng 1,5× người chơi; chu kỳ skill đo được 3,5 s; hạ boss thì lưu checkpoint và sang giai đoạn quái thường kế. `SoloBossChecks`: `ValidateModel`, `ValidateDefinition`, `ValidateRuntime` (cả lộ trình), `ValidateAttempts` đều PASS.
5. **Chưa kiểm tra**: nhạc (không nghe được), chữ trên màn thua (`TRIES LEFT` / `OUT OF TRIES!`) vì HUD không nằm trong ảnh chụp camera, và chơi thật đủ 1:30 + 1:00 bằng tay (đồng hồ bị bỏ qua khi test).
