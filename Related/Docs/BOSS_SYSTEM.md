# Hệ thống boss (dùng cho nhiều boss)

Code chung không biết tên boss. Một boss mới (boss phụ, boss map mới) = art + một `BossDefinition` + vài `BossSkillDefinition`. Chi tiết từng trận: [SOLO_BOSS_ENCOUNTER.md](SOLO_BOSS_ENCOUNTER.md). Số liệu từng quái: [Monster_Infomations.md](Monster_Infomations.md).

## Các phần của hệ thống

| Thành phần | Việc làm | Tệp |
| --- | --- | --- |
| `BossDefinition` | Dữ liệu một boss: tên, animation, thời gian quái thường trước boss, các segment máu, kích thước, cảnh vào, thông số bệ | `Scripts/Gameplay/Boss/BossDefinition.cs` |
| `BossSkillDefinition` | Một đòn: clip của boss, frame va chạm, thời gian cảnh báo, loại hiệu ứng, kích thước theo chiều cao người chơi | `Scripts/Gameplay/Boss/BossSkillDefinition.cs` |
| `BossAnimationSet` | Các clip có tên (idle, hurt, skill, hiệu ứng...) dựng từ manifest | `Scripts/Gameplay/Boss/BossAnimationSet.cs` |
| `BossVisual` | Phát clip lên sprite thân boss (nghỉ, hurt, death, skill), nhịp theo `Tick` nên Pause đứng yên | `Scripts/Gameplay/Boss/BossVisual.cs` |
| `BossSkillCaster` | Chọn skill ngẫu nhiên (không lặp liền nhau), cảnh báo, ra đòn, hủy khi nổ bóng của boss | `Scripts/Gameplay/Boss/BossSkillCaster.cs`, `BossSkillBag.cs` |
| `IBossEntrance` | Cảnh boss xuất hiện. Có hai bản: `BossEntrance` (Ember) và `DullahanEntrance` (bệ đá) | `Scripts/Juice/` |
| `BossFxSprite` | Sprite hiệu ứng theo frame, có pool, không có `Update` | `Scripts/Juice/BossFxSprite.cs` |
| `SoloBossProgress` | Trạng thái một giai đoạn: đồng hồ quái thường, máu từng segment | `Scripts/Gameplay/SoloBossProgress.cs` |
| `SoloBossEncounter` | Điều khiển cả lộ trình: các giai đoạn, thanh HUD, bóng quanh boss, lượt chơi, checkpoint | `Scripts/Gameplay/SoloBossEncounter.cs` |
| `MusicDirector` | Nhạc menu, quái, boss xuất hiện, boss chiến đấu (crossfade khi đổi) | `Scripts/Gameplay/MusicDirector.cs` |

## Lộ trình và giai đoạn

`SoloBossEncounter` chạy một chuỗi **giai đoạn**. Mỗi giai đoạn gồm:

1. **Quái thường có hẹn giờ** (`BossDefinition.SurviveSeconds`). Thanh tiến trình đầy theo thời gian thực. Chỉ tính khi game đang ở trạng thái `Playing`, nên Pause đứng yên.
2. **Dọn sạch màn hình** (phase `Clearing`): hết giờ thì dừng sinh quái, đợi người chơi nổ hết quái còn lại.
3. **Boss xuất hiện**: nhạc chuyển sang nhạc boss; thanh đổi thành thanh máu boss (refill trong `RefillSeconds`); cảnh vào chạy theo `EntranceBeforeRefill` (xem dưới).
4. **Đánh boss**: 5 bóng quanh boss; nổ bóng gây 10 máu; skill tự ra theo `castInterval`.
5. Boss chết: nếu không phải boss cuối thì lưu **checkpoint** và sang giai đoạn kế; boss cuối thì thắng game (`GameManager.CompleteSoloBossRound`).

Danh sách giai đoạn: các boss trong mảng `_preludeBosses` (mỗi phần tử là một `BossStage`) theo thứ tự, rồi boss chính là các field `_boss`, `_bossRoot`, `_visual`, `_caster`, `_entrance` của `SoloBossEncounter`. Hiện tại: Dullahan Guard rồi Ember Drake Tyrant. Muốn thêm boss giữa chừng thì thêm một phần tử vào `_preludeBosses`.

## Lượt chơi và checkpoint

Mỗi màn có `SoloBossEncounter.MaxAttempts` = **5 lượt**; một lượt kết thúc khi mất hết 3 tim.

- Thua khi còn lượt: nút TRY AGAIN, lượt kế bắt đầu ở **giai đoạn checkpoint**: giai đoạn 0 nếu chưa hạ boss phụ nào, hoặc giai đoạn sau boss phụ gần nhất đã hạ.
- Thua lượt thứ 5: GAME OVER thật, nút PLAY AGAIN, chơi mới từ đầu với đủ 5 lượt, checkpoint bị xóa.
- Về menu chính, Restart trong Pause, hoặc thắng: luôn bắt đầu một lần chơi mới.
- Checkpoint và số lượt chỉ tồn tại trong phiên (không lưu ra đĩa).

HUD hiển thị `ATTEMPT n/5` ở cuối nhãn trên thanh. `MenuManager` đọc `AttemptsLeft` để ghi số lượt còn lại và chọn nút TRY AGAIN hay PLAY AGAIN.

## Cảnh vào (`IBossEntrance`)

`Prepare` đặt sẵn các thứ tĩnh, `SetRise(t)` chạy trong lúc thanh refill, `Run` là phần diễn, `Cancel` dọn. Thứ tự do `BossDefinition.EntranceBeforeRefill` quyết:

- `true` (Ember): cảnh vào diễn trước, thanh refill sau.
- `false` (Dullahan): thanh refill trong `RefillSeconds` trong khi `SetRise` nâng bệ đá, rồi boss nhảy vào.

Các clip cảnh vào nằm trong animation set của boss. `BossEntrance` dùng `whirlwind`, `pillar`, `roar`. `DullahanEntrance` dùng `platform_idle`, `platform_landing`, `landing_dust`, `stone_debris`, `jump`, `land`; thiếu clip nào thì bỏ qua phần đó.

Quy tắc kích thước: skill cao `PlayerHeightMultiple` × chiều cao người chơi (đang đặt 1,5). Boss luôn nằm trên đầu người chơi: `HoverPosition` nâng điểm đứng lên đến khi đáy boss cao hơn đỉnh đầu người chơi 0,25 đơn vị.

## Thêm boss mới

1. **Art**: sprite sheet lưới đặt trong `Assets/Sprites/bosses/<map>/<boss_id>/`. Thư mục này tự nhận rule import: Point, không nén, Max Size 4096 (`Editor/SpriteImportSettings.cs`).
2. **Manifest clip** `<boss_id>_clips.json` (cùng thư mục):
   ```json
   { "ppu": 100, "sheets": [
     { "file": "boss_sheet.png", "cell_w": 512, "cell_h": 512, "columns": 8,
       "clips": [ { "key": "idle", "frames": [0,1,2,3], "ms": 120, "loop": true } ] } ] }
   ```
   `frames` là chỉ số ô theo hàng, từ trái qua phải, từ trên xuống. `ms` là thời gian mỗi frame (hoặc `frame_ms` cho từng frame).
3. Chọn manifest trong Project rồi chạy `Tools > Magic Touch > Boss > Build Animation Set From Selected Manifest`: tạo `<boss_id>_clips_Animations.asset`.
4. **Skill**: `Assets > Create > Magic Touch > Boss > Skill`. Chọn `kind` (`Projectile`, `GroundSpike`, `Slash`), clip của boss, frame va chạm, clip hiệu ứng, `Player Height Multiple` và `Content Height` (phần cell mà hình thật chiếm).
5. **Boss**: `Assets > Create > Magic Touch > Boss > Definition`: animation set, **thời gian quái thường** (`Survive Seconds`), **các segment máu** (máu, màu thanh, clip nghỉ, danh sách skill, chu kỳ), số bóng quanh boss, scale, `Feet Offset`, `Body Half Height`, và cảnh vào.
6. Dựng object boss trong scene (root, `EnemyBody` có `SpriteRenderer` + `BossVisual`, `BossSkillCaster`, và một component `IBossEntrance`). Cách nhanh: sao chép khối trong `Editor/SoloBossSetup.cs` (có menu cho từng boss) rồi gán vào `_preludeBosses`.

Mini boss: 1 segment, scale vừa, bệ đá hoặc cảnh vào riêng.

## Khóa clip bắt buộc

`idle` (hoặc clip nghỉ của từng segment), `hurt`, `death`, các clip skill (do `BossSkillDefinition` trỏ tới) và clip hiệu ứng của skill. Clip cảnh vào chỉ bắt buộc với loại cảnh vào được dùng (xem trên).

## Quy ước art

512×512 mỗi frame (boss), khối pixel 2×2, bảng màu ≤ 32 màu, alpha cứng. Hiệu ứng cùng mật độ pixel với boss ở scale 1.

**Pivot**: mỗi sheet trong manifest có `pivot_x`/`pivot_y` (0..1, mặc định giữa 0,5/0,5). Boss bay (Ember) dùng pivot giữa. Boss đứng trên bệ (Dullahan) dùng pivot ở chân (`pivot_y` 0,0625: chân nằm ở dòng 480 của ô 512), nên gốc của object boss chính là mặt đất và `Feet Offset` đặt 0. **Mọi sheet thân boss (idle, skill, nhảy, tiếp đất, hurt, death) phải cùng pivot**; sheet hiệu ứng và bệ dùng pivot giữa.

## Âm nhạc

`MusicDirector` giữ 4 track: menu, quái thường, boss xuất hiện, boss chiến đấu. Nhạc quái thường là `CombatCastleMusic.wav`. Hết giai đoạn boss phụ thì quay về nhạc quái (`EndBossMode` rồi `PlayCombat`); thắng boss cuối thì fade out. Nhạc dài import `Streaming` (`Editor/AudioImportSettings.cs`). Âm thanh thắng/thua: `MenuManager.gameVictorySound` và `GameOver.wav`.

## Kiểm tra

`SoloBossChecks.ValidateModel()`, `ValidateDefinition()` (mọi boss), `ValidateRuntime()` (chơi hết lộ trình ở Play Mode, bỏ qua đồng hồ), `ValidateAttempts()` (5 lượt, checkpoint), `ValidateLifeLoss()`. Cũng có `Tools > Magic Touch > Boss` menu dựng lại từng boss; các menu này lưu scene nên chỉ bấm khi đã hỏi.
