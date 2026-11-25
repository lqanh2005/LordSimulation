# LordSimulation — ghi chú cho Claude

Game quản lý / city-builder / kiểm duyệt biên giới (PC, Steam). Người chơi là Thành Chủ một thành bang biên giới:
kiểm duyệt dân tị nạn tại Cổng (Admit / Deny / Quarantine), xây thành, phân bổ nhà & việc, sống sót qua mùa đông và dịch bệnh.
Tham khảo: Papers, Please · Frostpunk · Banished · Oxygen Not Included (HUD/alert).

**GDD đầy đủ:** `Docs/Chuyển sinh thành lãnh chúa _da trắng_.md` (v0.2). Đọc lại mục liên quan trước khi làm tính năng mới.

## Design pillars (luôn giữ)
1. Every Decision Has a Cost — không có lựa chọn hoàn toàn đúng.
2. Systems Create Consequences — hệ thống nối nhau thành chuỗi (thiếu than → chết cóng → xác → Miasma → dịch → Riot).
3. Manual to Automation — thủ công → Rules → Edicts → tự động hóa.
4. Growth Creates Pressure — mạnh hơn thì áp lực lớn hơn.

## MVP (Vertical Slice) theo GDD
- Gate: NPC có tên/nghề/nguồn gốc/triệu chứng; Waiting Yard; 1 bệnh chính Sốt Đỏ; quyết định ảnh hưởng Population / Reputation / Disease Risk.
- Building: grid 1x1/2x2/3x3, Road, House, Food, Coal, Storage, Quarantine Station, Clinic.
- Tài nguyên: Food, Coal (sưởi mùa đông), Wood (xây), Gold, Medicine.
- Edict MVP: tối đa 3 rule, IF + AND, action Admit/Deny/Quarantine; ưu tiên trên→dưới, rule đầu tiên khớp được chạy; UI phải trả lời "Why was this NPC processed?".
- Tech: chỉ Tier 0 → Tier 1.
- Crisis: Winter/Snowstorm (chính), Disease Outbreak (phụ).
- Trade: thương nhân thỉnh thoảng tới Cổng, mua/bán Food/Coal/Wood.
- **Win:** sống sót qua mùa đông đầu tiên (có thể thêm Population ≥ 20). **Lose:** Population = 0 hoặc Riot = 100%.
- Quarantine mặc định 3 tháng (balance TBD).

## Thời gian
1 năm = 12 tháng, 1 mùa = 3 tháng, **tháng là đơn vị mô phỏng** (tiêu thụ, sản xuất, thuế, bệnh, nghiên cứu, cách ly, thương mại).
Gate Inspection chạy realtime nhưng không làm dân già đi.
**Vòng ngày–đêm chỉ để hiển thị (kiểu Cities: Skylines II): 1 tháng = 1 vòng ngày–đêm** (120 giây ở x1), không đếm "ngày thứ mấy".
Giờ suy từ `timeTickProgress` trong `Data/GlobalSystem/DayCycle.cs` (đầu tháng = 6h). Lịch sinh hoạt cư dân ở `ResidentManager.ResolveCommuteGoal`:
đi làm 7–17h / học 8–15h (xuất phát sớm theo quãng đường, lệch giờ theo từng người), ~1/5 lính gác & thầy thuốc làm ca đêm,
người không đi làm ra quảng trường (Tòa thị chính) ban ngày, đêm về nhà thì ẩn hình (`ResidentBase.SetIndoors`), vô gia cư đêm tụ quanh lửa trại.
Dân đi bộ nhanh theo tốc độ game (`GlobalSystemManager.SpeedMultiplier`). Ánh sáng: **URP 2D Renderer** (`PC_Renderer2D` / `Mobile_Renderer2D` trong `Assets/TextMesh Pro/Settings`, renderer 3D cũ giữ ở index 1) + Global Light 2D do `UI/DayNightOverlay.cs` dựng lúc chạy, đổi màu/cường độ theo giờ (ánh sáng nhân vào sprite nên giữ tương phản; màu chỉnh trên Inspector). Sprite thế giới dùng `Sprite-Lit-Default` (SpriteRenderer tạo bằng code tự nhận); thứ phải luôn rõ ban đêm (icon trạng thái, ô đặt công trình) gọi `DayNightOverlay.MakeUnlit`. Đèn cục bộ (lửa trại, Xưởng lò, Lò rèn, nhà dân, nhà lớn): `Data/Building/BuildingNightLight.cs` + Point Light 2D trong prefab, gắn bằng *LordSim/Art/5. Add Night Lights* (bảng `ArtAssetSync.NightLights`). `PlacementManager.RefreshNightLights` bật/tắt mỗi 0.5s: xây xong + đang bật, nhà phải có người ở, nơi làm việc phải có công nhân. Icon mặt trời trên HUD đổi màu.
Xếp nhà/việc ngay khi có thay đổi: `ResidentManager.RequestReassign()` (gom về LateUpdate) — gọi khi có người vào thành, công trình xây xong (`BuildingCompleted`), bật/tắt công trình.

## Điều khiển theo GDD
WASD / giữ chuột giữa: pan · Wheel: zoom · Space: pause · Tab: x1/x2/x3 · R: xoay khi đặt · Delete: chế độ phá ·
F5: quick save · G: Cổng · H: trung tâm · T: Tech Tree · E: Edicts · M: World Map · N: News · 1-4: nhóm xây dựng · Esc: đóng / Pause.

Đã làm trong `Controller/GameHotkeys.cs` (mọi phím tắt gom về đây, Esc cũng do file này điều phối):
Space, Tab, F5, Delete (X vẫn dùng), G, H, T, E, 1..N (theo số nhóm trên thanh xây dựng, hiện 4: Nhà ở / Sản xuất / Nông nghiệp / Hạ tầng), Esc.
Phím debug (chỉ Editor / Development Build): F9 thử âm báo Info → Warning → Critical.
Camera (`Map/IsoTestCamera.cs`) bị giới hạn trong bản đồ: tâm camera kẹp theo toạ độ ô trong `[-mapMargin, Width-1+mapMargin]` (LateUpdate, nên cả nhảy tới công trình cũng bị kẹp); tắt bằng `clampToMap`.
Chưa có: R (xoay — cần sprite xoay), M (chưa có World Map), N (chưa có News).

## UI theo GDD
- Alert 3 mức: Info / Warning / Critical; click alert → nhảy tới đối tượng liên quan.
- Panel công trình: Status, Workers, Input/Output, Production rate, Efficiency, Storage, Upgrade/Enable/Disable/Demolish; nếu không chạy phải nói lý do ("Not Working — No Workers").
- Panel cư dân: Name, Profession, Health, Home, Workplace, Happiness, Current Status (Homeless/Unemployed/Sick/Quarantined).

## Quy ước code
- Unity **6000.3** (GDD ghi 2022.3 — đã lệch, project thật dùng Unity 6). Input System mới (`Keyboard.current`, `Mouse.current`), TextMeshPro.
- Dữ liệu dạng struct mảng cố định + Manager (`ResidentManager`, `BuildingManager`, `GlobalSystemManager`, …) + static `*Rules` / `*System` cho logic thuần.
- Cư dân trong mảng là **mẫu đại diện**; `GlobalSystemData.totalPopulation` là dân số thật (macro). Chết/sinh trong mẫu được quy tỉ lệ ra macro.
- Giao tiếp qua `GameEvents.Post/Listen(EventID.X, payload)`; payload struct khai báo trong `Base/EventID.cs`.
- Popup nhỏ / toast tự dựng UI lúc chạy bằng `UI/RuntimeUIKit.cs` (không cần sửa scene); khởi tạo trong `PlacementManager.Start`.
- Thắng/thua kiểm tra cuối mỗi tháng trong `Data/GlobalSystem/GameOverChecker.cs` (ngưỡng chỉnh trên Inspector); popup chung `UI/GameOverPopupController.cs`. Cờ `victoryAchieved` lưu ở cuối file save (v7).
- Thương nhân: logic `Data/TradeRoute/MerchantManager.cs` (trên PlayerContain, tự thêm trong `PlayerContain.Init`), UI `UI/MerchantPopup.cs`; trạng thái lưu cuối save (v8). `TradeManager`/`TradeRouteData` là khung tuyến thương mại cho Full game, chưa dùng.
- Hàng chờ ở Cổng: `ImmigrationManager.ProcessWaitingYard` mỗi tháng (ăn lương thực thành, đói -> bỏ đi & mất uy tín, lây bệnh, đông -> bạo loạn); hằng số trong `ImmigrationRules` (mục Waiting Yard). Người chờ dùng `hungerMonths` làm số tháng đói trong hàng (không đổi format save).
- Vô gia cư (`assignedHouseID < 0`): trừ hạnh phúc + góp bạo loạn (`ResidentSocialRules`), tăng nhiễm bệnh (`ResidentDiseaseSystem`), mùa đông luôn bị lạnh (`ResidentManager.ProcessMonthlyColder`).
- Đường (`BuildingType.Road`): ô đường ghi id nhưng không chặn đi lại (`GridController.SetRoad`); kéo chuột để vẽ liên tục. Kết nối tính trong `Data/Building/RoadNetwork.cs` (hub = Tòa thị chính đã xây xong; chỉ công trình sản xuất cần nối; chưa có / đang xây Tòa thị chính thì miễn). Gọi `RoadNetwork.MarkDirty()` khi đặt / phá / dựng lại công trình.
- Trạng thái + lý do công trình không chạy: `Data/Building/BuildingStatusRules.cs` (dùng cho popup công trình). Tạm dừng / chạy lại = bật tắt `BuildingData.isOperational`.
- Chuỗi khủng hoảng: `Data/GlobalSystem/CrisisManager.cs` — thi thể (`corpsesFresh/corpsesRotting`) → Miasma → cộng áp lực dịch bệnh + bạo loạn; lưu save v9. Lính gác / thầy thuốc thu gom thi thể.
- Bão tuyết: `GlobalSystemManager.TryStartWeatherEvent` — chỉ mùa đông, xác suất/thời lượng/than thêm chỉnh trên Inspector; `weatherDuration` = số tháng còn lại tính cả tháng hiện tại.
- Cách ly: `ResidentData.quarantineMonths` (save v10) đếm ngược trong `ImmigrationManager.ProcessQuarantineUpkeep`; >0 thì `NeedsQuarantineHousing` giữ ở Trạm cách ly, hết hạn mà khoẻ thì ra thành. Mặc định `ImmigrationRules.DefaultQuarantineMonths` = 3 hoặc lấy từ sắc lệnh. Không còn chỗ trong trạm thì nút Cách ly bị chặn. Người cách ly chỉ tốn thêm thuốc (lương thực đã tính chung).
- Từ chối ở Cổng: `ImmigrationRules.GetDenyReputationChange` — người khoẻ bị trừ uy tín, có triệu chứng rõ thì cộng nhẹ.
- Sắc lệnh: ELSE chỉ dùng khi không luật nào khớp IF (`EdictRules.TryResolveImmigration`, `usedElse`).
- Sắc lệnh bị khoá tới khi nghiên cứu `ResearchID.CodeOfLaw` (Bộ luật, Tier 1) — `EdictRules.EdictsUnlocked`; sắc lệnh có sẵn trong save cũ vẫn chạy.
- Thi thể: `CrisisManager.AddCorpses(n)` (cư dân chết qua `ResidentsDied`, người chết trong hàng chờ gọi trực tiếp); xử lý ở LateUpdate sau tick, xác tươi chỉ phân hủy nếu còn nằm lại sang tick sau.
- Dân số: `RefreshMetricsCache` giữ `totalPopulation >= số mẫu còn sống`, hết mẫu = 0 dân. Uy tín âm tự hồi `ImmigrationRules.ReputationRecoveryPerMonth`/tháng, lượng người tới không dưới `MinArrivalMultiplier`.
- Sắc lệnh thành phố có giá (`EdictRules.GetMonthlyGoldCost/DescribeCost`, áp trong `EdictManager.ApplyMonthlyEffect`): thu thuế/tăng sản xuất -> +bạo loạn, xoa dịu/uy tín/thêm tài nguyên -> tốn vàng, thiếu vàng thì tháng đó không hiệu lực.
- `overallHappiness` lưu theo % (0–100); hạnh phúc từng cư dân vẫn 0–1.
- HUD tài nguyên: dòng "/th" là thay đổi ròng của lần mô phỏng tháng gần nhất (`GlobalSystemManager.LastMonthNet`, chụp kho trước/sau `AdvanceToNextMonth`; không tính xây / mua bán thủ công, không lưu save). Dương xanh, âm đỏ, 0 xám.
- Xe goòng là một phần art của Mỏ (child `Cart` trong `CoalCave.prefab`, *LordSim/Art/6. Attach Cart To Mine*), không còn trên thanh xây dựng; `BuildingType.CoalCart` giữ cho save cũ (vẫn là "trang trí" giảm bạo loạn trong `BuildingEffectRules`). Hiệu ứng giảm bạo loạn chuyển cho Giếng nước (`WellRiotReduction` 0.3/tháng mỗi giếng, chung trần `DecorRiotReductionMax` = 2).
- Nước uống: `Data/GlobalSystem/WaterRules.cs` — suối cấp sẵn cho `BaseWaterCapacity` (10) người, mỗi Giếng đang hoạt động +`WellWaterCapacity` (15). Mỗi tháng `GlobalSystemManager` tính `LastThirstyRate` (dân vượt sức cấp nước) -> +bạo loạn, +áp lực dịch bệnh (nước bẩn), post `EventID.ThirstResolved` -> `ResidentManager.ProcessMonthlyThirst` (giống đói: trừ hạnh phúc, `ResidentData.thirstMonths` lưu save v11, khát >= 2 tháng có thể chết, `DeathCause.Thirst`). Khát cũng chặn sinh con, giảm tài sản, tăng tử vong tuổi già/bệnh. UI: thông báo "Thiếu nước" (Critical khi >= 34%) + "Nước sắp không đủ", popup Giếng hiện nước toàn thành, popup cư dân "Khát N tháng". `GlobalSystemManager.CurrentWaterCapacity` cho UI.
- Công trình chế biến tốn nguyên liệu (`ResidentProductionRules.TryGetInput`): Xưởng lò & Lò rèn đốt than, Kho than đốt gỗ ra than; thiếu thì popup báo "thiếu …".
- Áp giải thẩm vấn: tốn `authorityPoints` (hồi `ImmigrationRules.AuthorityRegenPerMonth`/tháng), lộ bệnh thật kể cả đang ủ — `ImmigrationManager.Interrogate`.
- Nghiên cứu tốn vàng lúc bắt đầu (`ResearchCatalog.GoldPerResearchPoint`), đổi/huỷ mất vàng; điểm dư chuyển sang công nghệ kế.
- Cổng: Nhiệt kế (`ResearchID.Thermometer`, Tier 1) mới thấy thân nhiệt và dùng được điều kiện Thân nhiệt trong sắc lệnh (`EdictRules.IsVariableUnlocked`). Tối đa `EdictManager.RuleLimit` = 3 sắc lệnh. Dòng "Vì sao" do `ImmigrationManager.ExplainSuggestion`.
- Thông báo: `NotificationFeed.Show(msg, level, key, onClick)` — 3 mức Info/Warning/Critical, `onClick` dùng `UI/UIActions.cs` để tới nơi liên quan. Mỗi mức có âm riêng qua `MusicManager.PlayAlert` (ô Alert Info / Warning / Critical trên MusicManager ở LoadingScene; để trống = im lặng, hiện chưa gán clip nào), nhiều thông báo cùng lúc chỉ kêu 1 lần.
- Cài đặt `UI/SettingsPopup.cs` (mở từ Pause Menu, PlayerPrefs), nhạc/hiệu ứng `Controller/MusicManager.cs` (chưa có file âm thanh — gán clip trong Inspector), hướng dẫn `UI/TutorialHints.cs`.
- Khi chụp màn hình Play mode qua MCP mà Unity ở nền, chữ tiếng Việt có thể thiếu dấu do chưa vẽ lại — gọi `ForceMeshUpdate` trên các TMP_Text trước khi chụp, không phải lỗi font.
- Đổi format save → tăng `CitySaveSerializer.SaveVersion` và đọc có điều kiện theo version để save cũ vẫn mở được.
- **Skin UI theo mockup `Assets/UI/`** (nền tối, viền vàng `#B38B4D`, chữ kem `#F3E6BC`): sprite + màu + font gom trong `UI/UISkin.cs` (asset `Resources/UISkin.asset`). Thay ảnh trong `Assets/UI` → chạy menu *Tools/LordSimulation/UI Skin/1. Configure Sprite Import* rồi *2. Create Or Update Skin Asset*. UI có sẵn trong scene được áp skin tại chỗ bằng *UI Skin/Apply All* (`Editor/UISkinApplier*.cs`, toạ độ theo mockup 1920x1080, chạy lại an toàn). UI dựng lúc chạy dùng `UI/UIWidgets.cs` (cửa sổ, tab, slider, checkbox, dropdown) và `RuntimeUIKit.SetButtonStyle`. Sprite phụ sinh bằng code nằm ở `Assets/UI/Generated`. Font: Noto Serif (tiêu đề/nút, `UISkin.titleFont`) + Inter (nội dung, `bodyFont`) ở `Assets/Fonts` (SIL OFL, giữ file license khi phát hành), TMP Dynamic + fallback LiberationSans — tạo bằng *UI Skin/3. Create Font Assets*.
- **Addressables** (package 4.2): nạp qua `Base/GameAssets.cs` (`LoadAsync/LoadSync/Release` theo address, giữ handle). Group *UI*: `UISkin` (`Assets/Addressables/UI/UISkin.asset`, không còn ở Resources) + ảnh nền Cổng (nạp khi mở, Release khi đóng). Group *Music*: mỗi bài một bundle, `MusicManager` chỉ giữ bài đang phát. Thêm/đổi asset → *Tools/LordSimulation/Addressables/Setup Groups*. Build game tự build Addressables (BuildWithPlayer). `MapData` vẫn ở Resources (nhỏ, tool bản đồ ghi đúng đường dẫn đó). Ảnh UI ≥ 512x512 nén BC7/DXT.
- Popup thông tin cư dân / công trình / nông trại cố định ở góc phải dưới canvas (`RuntimeUIKit.PlaceBottomRight`), không bám theo đối tượng nữa. Click ưu tiên công trình: ô có công trình (trừ đường, `PlacementManager.GetSolidBuildingIdUnderMouse`) thì popup cư dân nhường; trên đường thì cư dân được ưu tiên.
- Tiếng click UI: `UI/UIClickSound.cs` (MusicManager tự gắn) bắt mọi click chuột trái trên Selectable đang bật (Button/Toggle/Dropdown, cả nút trong scene lẫn dựng lúc chạy) — **không** gắn `RuntimeUIKit.PlayClick` vào onClick nữa (sẽ kêu 2 lần).
- Hiệu ứng popup: `UI/UIPopupAnim.cs` trên gốc popup (lớp phủ). Mở/đóng bằng `UIPopupAnim.Show(go)` / `UIPopupAnim.Hide(go)` thay cho `SetActive`, kiểm tra mở bằng `UIPopupAnim.IsVisible(go)` (đang đóng dở coi như đã đóng). Cửa sổ dựng bằng `UIWidgets.CreateWindow` tự có. Chuyển scene: `Base/Fader.cs` (màn che + logo + 3 hình thoi, load async), tốc độ = `fadeSpeed` trên GameController.
- Hộp thoại xác nhận / cảnh báo: `ConfirmDialog.Ask(...)` / `ConfirmDialog.Alert(...)`. Lưu nhiều ô: `SaveLoadManager.SlotCount` (ô 1 = file save cũ), cửa sổ `UI/SaveLoadWindow.cs`; meta ngày trong game ghi ở file `.meta` cạnh save. Lịch sử thông báo: `NotificationFeed.History` + `UI/NoticeWindow.cs` (nút chuông).
- Project **tắt Domain Reload** (Enter Play Mode Options): biến static không tự reset giữa các lần Play / đổi scene. Static có trạng thái phải reset bằng `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)] ResetStatics()` (xem `EdictRules`, `RoadNetwork`, `GameAssets`, `UISkin`, `SaveLoadManager`); hook static trỏ vào MonoBehaviour thì gỡ trong `OnDestroy` (`ResearchManager`).
- Cảnh quan quanh thành (nền đất, tường, cổng, núi, cây, phế tích): object `MapScenery` trong GamePlayScene, dựng bằng *LordSim/Art/7. Build Map Scenery* (`Editor/MapSceneryBuilder.cs`, bố cục `Editor/MapSceneryLayout.txt` theo pixel của `Decor/map_Mockup.jpg`; chạy lại sẽ xoá chỉnh tay trong `MapScenery` và ghi lại ô đất + `GateCell` trong `MapData`). Thứ tự vẽ: sau thành < 100, trong thành 100–218, tường trước 230+, trước thành 300+. Cổng thật là `MapScenery/CityGate`. Camera không ra ngoài nền nhờ `IsoTestCamera.viewBounds`.
- Dung lượng texture: cỡ import + cài đặt atlas (trang 2048, xếp chặt, Atlas_Map có mipmap) do *LordSim/Art/8. Optimize Texture Sizes* quản lý (`Editor/TextureSizeOptimizer.cs`, không sửa file gốc). Thêm ảnh mới trong `Assets/Texture/Map|Building|Farm` thì chạy lại.
- Comment và text UI viết **tiếng Việt**.
- Có Unity MCP (`mcp__unityMCP__*`) để compile, đọc console, chạy Play mode, chụp màn hình.
