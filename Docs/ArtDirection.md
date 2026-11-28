# LordSimulation — Hướng Art (Art Direction) v2

*Cập nhật 27/11/2026: **hậu tận thế hiện đại**.* Bản v1 (trung cổ thuần) đã bị huỷ.
Chuẩn tham chiếu duy nhất: concept bản đồ `Assets/Texture/Map/Decor/map_Mockup.jpg`.
Tham chiếu phụ: sprite cư dân (`Assets/Texture/Resident`), skin UI (`Assets/UI/*_Mockup.png`).

## 1. Một câu tóm tắt

> Một thành nhỏ do người sống sót dựng lại bên trong tường đá, giữa rừng núi đã mọc lấn lên tàn tích của thế giới hiện đại. Thiên nhiên tươi, nhưng mọi thứ con người làm ra đều cũ, vá víu, tận dụng từ đống đổ nát.

Ý cốt lõi: **thiên nhiên mới — đồ vật cũ.** Cây cỏ, núi, sông vẽ tươi như concept. Công trình, đạo cụ, quần áo luôn có dấu vết tái sử dụng.

## 2. Luật cứng

1. **Mọi thứ do người làm đều phải "đã qua sử dụng".** Không có vật nguyên mới, sơn bóng. Mỗi công trình có ít nhất 2 dấu hiệu chắp vá: tấm tôn ghép, ván bít cửa sổ, dây buộc, bạt che, gỉ sét, vết nứt, vật liệu không cùng bộ.
2. **Công nghệ đi theo tier** (GDD: lều → nhà gỗ → hơi nước → công nghiệp → hiện đại). Đồ hiện đại ở tier thấp chỉ được xuất hiện dưới dạng **nhặt được, hỏng một phần** (pin mặt trời nứt buộc dây, thùng phuy làm bếp), không phải công trình hiện đại hoạt động trơn tru.
   - **Tier 0 — Sống sót:** bạt, lều, pallet, thùng phuy, tôn, lốp xe, cọc gỗ, bao cát.
   - **Tier 1 — Dựng lại:** gỗ xẻ, gạch/bê tông nhặt lại xếp chồng, container, mái tôn, ống khói ghép từ ống kim loại, đồ điện cũ hồi phục (đèn, máy phát nhỏ).
   - Tier 2+ (ngoài MVP): hơi nước, công nghiệp — chưa vẽ.
3. **Chữ trên sprite:** không vẽ chữ mới. Biển hiệu cũ của thế giới trước (cửa hàng, biển đường) **được phép** nếu chữ mờ, bong tróc, không đọc rõ thành câu, và **chỉ tiếng Anh** (chữ còn sót lại). Chức năng công trình nhận biết bằng hình (chữ thập đỏ sơn tay, cờ vàng dịch bệnh, đống than…), không bằng chữ.
4. **Nét vẽ theo concept:** vẽ tay, có viền tối, đổ bóng phẳng 2–3 tầng (như cây, núi, tường, tòa đổ trong concept). **Không** dùng phong cách vector phẳng không viền với gradient như bộ công trình hiện tại.
5. **Ánh sáng vẽ sẵn** từ **trái-trên**, bóng đổ phải-dưới, không đổ ra ngoài footprint.
6. **Màu báo trạng thái** (đỏ nguy, tím bệnh, xanh rét) chỉ dành cho icon, không phủ màu lên nhân vật/công trình.

## 3. Bảng màu (đo từ `map_Mockup.jpg`)

| Nhóm | Màu | Dùng cho |
|---|---|---|
| Cỏ trong thành | `#5B960E` `#7DAC34` `#5A7528` | Ô cỏ (ô hiện tại `#5A8A20` đã khớp) |
| Đất trong thành | `#835645` `#90684F` | Ô đất, đường đất (ô hiện tại `#9F7769`–`#B08970` đã khớp) |
| Cỏ / đồi ngoài thành | `#8F8D5E` `#ADB278` `#B2CC7D` `#7F714C` | Nền quanh thành, khô và vàng hơn trong thành |
| Lá cây | `#A2BF5B` `#7EB956` `#859856` `#4E4B39` (bóng) | Cây, bụi |
| Đá / núi | `#D4BFA7` `#BB9E87` `#88795D` `#615245` | Núi, tảng đá |
| Tàn tích hiện đại | `#96AB96` (kính) `#616C72` `#4B4D56` (bê tông, thép) | Tòa đổ, đường nhựa, cổng sắt |
| Nước | `#ACD3B4` `#87A99B` | Sông, giếng |
| Bóng tối chung | `#33372A` `#443831` | Viền và vùng tối |
| Điểm nhấn *(đề xuất, chưa có trong concept)* | Gỉ sét `#9A5A35` · Bạt xanh `#4F7A8C` · Cờ dịch vàng `#D9A62B` · Lửa `#E8A23A` | Dùng ít, để kể chuyện "chắp vá" |
| UI | Vàng viền `#B38B4D` · Kem `#F3E6BC` · Nền tối | Theo `UISkin` (xem mục 5 — UI) |

Quy tắc màu: đồ hiện đại còn sót (bê tông, thép, kính) dùng **xám lạnh**; đồ người sống sót làm (gỗ, bạt, gỉ) dùng **nâu/cam ấm**. Sự tương phản lạnh–ấm này giúp người chơi tách "thế giới cũ" và "thành của mình".

## 4. Tỉ lệ và kỹ thuật (giữ để không phải sửa code)

| Mục | Quy chuẩn |
|---|---|
| Ô nền | Hình thoi isometric **2:1** (ví dụ 400×200 px), PPU 400 |
| Công trình | PPU 400; chiều rộng sprite ≈ số ô × 400 px; pivot ở **đáy hình thoi footprint** |
| Cư dân | PPU 320, khung 248×320, nhân vật cao khoảng 176 px (≈ nửa chiều rộng 1 ô) |
| Cửa ra vào | Cao ≈ 1.2 × cư dân |
| Cây ngoài thành | Cao tối đa ≈ 2–3 ô |
| Kích thước file | **Cạnh chia hết cho 4** (nén BC7/DXT); ≤ 1024 px cho công trình, ≤ 2048 cho ảnh nền UI |
| Định dạng | PNG nền trong suốt, không halo trắng |

Footprint (`Editor/BuildMenuSetup.cs`): 1×1 Lều, Nhà dân, Trạm gác, Giếng, Lửa trại · 2×2 Trại gỗ, Nông trại, Mỏ, Lò rèn, Phòng khám, Trường, Khu cách ly, Kho than · 3×3 Nhà lớn, Xưởng lò, Tòa thị chính · 4×4 Vườn thuốc.

## 5. Danh sách asset

**P1** = người chơi thấy trong 15 phút đầu. **P2** = giữa ván. **P3** = trang trí / sau.

### Màn ngoài game (lệch nhiều nhất)

| Asset | Hiện trạng | Hướng xử lý | Ưu tiên |
|---|---|---|---|
| Nền Loading `UI/Loading/Back.png` + nền Home `UI/Home/0. UI-02_ MAIN MENU (MENU CHÍNH).jpg` | Làng trung cổ thuần: rào cọc, tháp gỗ, cờ, cầu gỗ, không có tàn tích | Vẽ lại: thành tường đá của concept, nhìn xa thấy tòa kính đổ phủ dây leo, đường cao tốc gãy, cầu bê tông sập; khói bếp bay lên từ trong thành | P1 |
| Nền Cổng `UI/Gate/Back_Gate.png` | Cổng thành gỗ-đá + cờ trung cổ, khác cổng sắt + bốt gác trong game | Vẽ lại đúng cổng trong game: cổng sắt (có tấm tôn hàn thêm), bốt gác đèn xoay, bao cát, rào thép gai, hàng người chờ phía sau | P1 |
| Logo (khiên, kiếm, lâu đài) | Thuần fantasy/trung cổ | **Cần quyết định** (xem mục 8). Nếu giữ: thêm chi tiết hậu tận thế (khiên làm từ biển báo giao thông, kiếm là thanh thép) | P1 |

### Công trình (`Assets/Texture/Building`, `Coal`, `Farm`)

Vấn đề chung của bộ hiện tại: đúng thời đại nhưng **mới tinh** và **sai nét vẽ** (vector phẳng, gradient, không viền). Cần vẽ lại cả bộ theo luật 1 và 4.

| Loại (`BuildingType`) | File hiện tại | Vấn đề | Hướng mới | Ưu tiên |
|---|---|---|---|---|
| Tent | `TemporaryTent.png` | Lều cắm trại sạch sẽ | Lều cứu trợ bạt xanh/xám vá, cột chống bằng ống thép, thùng phuy cạnh cửa | P1 |
| House | `Housing.png` | Nhà gỗ mới, pin mặt trời nguyên vẹn | Nhà gỗ ghép tôn, cửa sổ bít ván, pin mặt trời nứt buộc dây trên mái | P1 |
| CityHall | `CityHall.png` | Tòa văn phòng mới, chữ "Town Hall" | Một tòa cũ (bưu điện/trường học) được chiếm lại: kính vỡ bít bạt, cờ tự may trên mái, bao cát trước cửa, loa phóng thanh | P1 |
| Campfire | `Campfire.png` | Khối gạch sạch | Lửa trong thùng phuy cắt đôi hoặc vòng lốp xe/gạch vỡ, ghế làm từ thùng gỗ | P1 |
| WaterWell | `WaterWell.png` | Giếng mới, mái tôn cam bóng | Giếng đào lại: thành bê tông vỡ, ròng rọc ghép, bơm tay gỉ, mái tôn cũ | P1 |
| Farm | `Farm/Farm.png` | Nhà kho kiểu Mỹ | Lán dụng cụ tôn + hàng rào pallet, bồn chứa nước mưa | P1 |
| LumberCamp | *(chưa có art — khối màu)* | Thiếu | Bãi gỗ: giá cưa tay, đống củi, xe cút kít, rìu, cây đã đốn | P1 |
| Mine | `Coal/CoalCave.png` | Cửa hầm + nhà điều khiển mới | Cửa hầm gia cố bằng dầm thép và gỗ, đèn công trường cũ treo dây, đường ray gỉ | P1 |
| CoalStore | `Coal/CoalStore.png` | Bể bê tông, sọc vàng-đen mới | Container mở nắp hoặc bể bê tông nứt, sọc cảnh báo bạc màu, mái bạt | P1 |
| Road | *(khối màu)* | Thiếu art | Ô đường: nhựa vỡ lẫn đất nén, vạch sơn mờ đứt quãng, cỏ mọc khe nứt; nối được với đường ngoài thành trong concept | P1 |
| Clinic | `Hospital.png` | Bệnh viện gạch mới, chữ "Hospital" | Phòng khám dã chiến: tòa gạch cũ + lều y tế, chữ thập đỏ **sơn tay** trên tấm tôn, giường gấp | P2 |
| QuarantineWard | `QuarantineStation.png` | Đúng ý (biển nguy hiểm sinh học, rào chắn) nhưng quá sạch, có chữ tiếng Việt | Giữ ý: rào thép gai, lều cách ly, cờ vàng dịch bệnh, thùng hoá chất cũ; bỏ chữ | P2 |
| GuardPost | `GuardTower.png` | Tháp thép mới | Tháp ghép từ giàn giáo/cột điện, sàn ván, bao cát, đèn pha cũ | P2 |
| Forge | `Forge.png` | Gần ổn, có kính mái | Lò rèn lò gạch + đe là khối thép, mái tôn, phế liệu chất đống | P2 |
| Furnace | `Fireplace.png` | Lò công nghiệp mới | Lò cao tận dụng bồn chứa/ống khói nhà máy cũ, vá nhiều lớp | P2 |
| LargeHouse | `Housing_3x3.png` | Nhà mới, pin mặt trời | Dãy nhà ghép container chồng 2 tầng, cầu thang sắt, dây phơi đồ | P2 |
| School | `ResearchCenter.png` | Văn phòng kính, chữ "Phòng nghiên cứu" | Thư viện/phòng thí nghiệm cũ chiếm lại: kệ sách cứu được, ăng-ten tự chế, bảng đen | P2 |
| HerbFarm | `Farm/Farm_Drug` | Kiểm tra lại | Luống thuốc trong bồn/lốp xe, nhà kính ghép từ cửa sổ cũ | P2 |
| CoalCart | `CoalCart.png` | Xe goòng mới | Giữ hình, thêm gỉ và móp (chỉ là con của Mỏ) | P3 |

Ô ruộng `Dirt_*`, `Rice_*`, `Drug_*` **giữ lại**.

### Cảnh quan (`Assets/Texture/Map`, bố cục `Editor/MapSceneryLayout.txt`)

Cảnh quan **đã khớp concept** — đây là phần chuẩn nhất của game. Chỉ cần làm dày thêm câu chuyện.

| Asset | Hiện trạng | Hướng xử lý | Ưu tiên |
|---|---|---|---|
| `tòa.png`, `tòa bé.png`, `full tòa.png`, `đá quanh.png` | Đúng concept | **Giữ.** Thêm 1–2 biến thể có dây leo/cây mọc trên nóc để đỡ lặp | P2 |
| `đường dài*.png`, `CẦU.png` | Đúng concept | **Giữ.** Có thể thêm xe bỏ hoang dọc đường (đạo cụ riêng) | — |
| `cổng.png`, `bốt quan sát.png` | Đúng concept nhưng quá mới (bốt sơn kem sạch) | Làm cũ: gỉ, tôn hàn thêm vào cổng, bao cát, rào thép gai cạnh bốt | P2 |
| `tường trái/phải.png`, `cột1–4.png` | Đúng concept | **Giữ.** Biến thể sau: đoạn tường vá bằng tôn/bê tông | P3 |
| `GrassTile.png`, `DirtTile.png` | Màu đã khớp concept; ranh giới cỏ–đất răng cưa | Thêm ô chuyển tiếp cỏ↔đất (cạnh và góc) | P2 |
| `cây *.png`, `núi *.png`, `BaseDirt.png` | Đúng concept | **Giữ** | — |
| Đạo cụ tàn tích (mới) | Chưa có — ngoài tàn tích tòa nhà, thế giới cũ hầu như vắng | Xe hơi bỏ hoang, cột điện đổ, biển báo cong, thùng phuy, lốp xe, container gỉ, bê tông vỡ có cốt thép | P1 |
| Đạo cụ trong thành (mới) | Bên trong tường trống trơn | Đống pallet, bao cát, dây phơi, xe cút kít, bụi cây, gốc cây | P2 |

### Cư dân và Cổng

| Asset | Việc cần làm | Ưu tiên |
|---|---|---|
| Sprite cư dân (Kid / Young / Old) | **Giữ.** Biến thể sau: thêm 1 món đồ hiện đại cũ cho mỗi kiểu (ba lô, áo hoodie rách, khẩu trang, ủng cao su) | P3 |
| Chân dung ở Cổng | Đang phóng to sprite nhỏ trong game → vỡ hạt. Cần chân dung riêng ~512×640, biến thể theo tuổi/giới/nghề, trạng thái bệnh (mặt tái, phát ban, khẩu trang), trang phục người tị nạn | P1 |
| Icon trạng thái (`Cold/Flu/Homeless/Sick.png`) | Giữ ý tưởng; làm nhỏ hơn (~60%) | P2 |
| `Animation/NormalResident` (124 MB) | Không dùng ở đâu — xoá hoặc chuyển ra ngoài `Assets` | P1 (dọn dẹp) |

### UI

- Skin hiện tại (nền tối, viền vàng hoa văn, Noto Serif) mang cảm giác fantasy. **Tạm giữ** cho MVP.
- Nếu đồng bộ lại (P3): khung kim loại mòn có đinh tán / bìa giấy dán băng keo, vàng → đồng gỉ, giữ chữ kem cho dễ đọc. Chỉ cần thay sprite trong `UISkin`, không phải sửa layout.
- Thống nhất bộ icon (tài nguyên, nút cạnh phải, thanh xây dựng): tô đặc, viền mảnh, cùng tông kem.
- Badge số người chờ trên cổng (`MapScenery/CityGate/WaitingBadge`, đang là TextMesh debug) → biển tôn nhỏ sơn số theo UISkin. P2.

## 6. Không khí, ngày–đêm và mùa

- **Ban ngày** giữ độ tươi của concept. Sự u ám của chủ đề đến từ **ánh sáng và mùa**, không phải từ việc vẽ lại cỏ cây xám đi.
- **Đêm:** tông xanh lạnh, tối hơn hiện tại (chỉnh Global Light trong `UI/DayNightOverlay.cs` trên Inspector). Thế giới cũ hoàn toàn tối; chỉ thành có ánh lửa và đèn tự chế (`BuildingNightLight`) → nhấn mạnh "đốm sáng cuối cùng".
- Artist đánh dấu vị trí nguồn sáng (toạ độ pixel) trên công trình có cửa sổ/lửa/đèn để gắn Point Light 2D.
- **Mùa đông** (P2, vì Win = sống qua mùa đông đầu): bản tuyết cho ô nền, mái, tàn tích; giảm bão hoà toàn cảnh.
- **Dịch bệnh** (đề xuất): khi áp lực dịch cao, ánh sáng ngả vàng-lục nhẹ; khi có Miasma, thêm sương mù thấp quanh khu thi thể.

## 7. Quy trình thay asset (không phải sửa code)

1. Giữ **đúng tên file và thư mục** của asset cũ, ghi đè PNG. Asset mới (LumberCamp, Road, đạo cụ) đặt cùng thư mục và thêm vào bảng `GameplayArt` trong `Editor/ArtAssetSync.cs` (đạo cụ cảnh quan thì thêm vào `Editor/MapSceneryLayout.txt`).
2. Giữ PPU và pivot như mục 4.
3. Chạy lại menu *LordSim/Art* liên quan (dựng prefab, Night Lights, **7. Build Map Scenery** nếu đổi cảnh quan — lưu ý menu này xoá chỉnh tay trong `MapScenery`, **8. Optimize Texture Sizes**).
4. Chụp so sánh ban ngày, ban đêm và mùa đông trong Play mode trước khi chốt.

## 8. Thứ tự làm đề xuất

1. **Chốt logo**: giữ khiên-kiếm-lâu đài (tương phản "lãnh chúa kiểu cũ giữa thời tận thế") hay đổi. Quyết định này ảnh hưởng tông của màn Home và UI.
2. **Concept 1 trang** cho bộ công trình mới: 1 Nhà dân + 1 Lều + 1 đoạn tường + 1 tòa đổ + 1 cư dân đứng cạnh nhau đúng tỉ lệ, đúng nét vẽ của concept. Duyệt xong mới vẽ hàng loạt.
3. P1 màn ngoài game: nền Loading/Home và nền Cổng.
4. P1 công trình (Lều, Nhà, Tòa thị chính, Lửa trại, Giếng, Nông trại, Trại gỗ, Mỏ, Kho than, Đường) + đạo cụ tàn tích.
5. Chân dung Cổng.
6. P2 rồi P3.
