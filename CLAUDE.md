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

## Điều khiển theo GDD
WASD / giữ chuột giữa: pan · Wheel: zoom · Space: pause · Tab: x1/x2/x3 · R: xoay khi đặt · Delete: chế độ phá ·
F5: quick save · G: Cổng · H: trung tâm · T: Tech Tree · E: Edicts · M: World Map · N: News · 1-4: nhóm xây dựng · Esc: đóng / Pause.

Đã làm trong `Controller/GameHotkeys.cs` (mọi phím tắt gom về đây, Esc cũng do file này điều phối):
Space, Tab, F5, Delete (X vẫn dùng), G, H, T, E, 1..N (theo số nhóm trên thanh xây dựng, hiện 6), Esc.
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
- Đường (`BuildingType.Road`): ô đường ghi id nhưng không chặn đi lại (`GridController.SetRoad`); kéo chuột để vẽ liên tục. Kết nối tính trong `Data/Building/RoadNetwork.cs` (hub = Tòa thị chính; chỉ công trình sản xuất cần nối; chưa có Tòa thị chính thì miễn). Gọi `RoadNetwork.MarkDirty()` khi đặt / phá / dựng lại công trình.
- Trạng thái + lý do công trình không chạy: `Data/Building/BuildingStatusRules.cs` (dùng cho popup công trình). Tạm dừng / chạy lại = bật tắt `BuildingData.isOperational`.
- Chuỗi khủng hoảng: `Data/GlobalSystem/CrisisManager.cs` — thi thể (`corpsesFresh/corpsesRotting`) → Miasma → cộng áp lực dịch bệnh + bạo loạn; lưu save v9. Lính gác / thầy thuốc thu gom thi thể.
- Cổng: Nhiệt kế (`ResearchID.Thermometer`, Tier 1) mới thấy thân nhiệt và dùng được điều kiện Thân nhiệt trong sắc lệnh (`EdictRules.IsVariableUnlocked`). Tối đa `EdictManager.RuleLimit` = 3 sắc lệnh. Dòng "Vì sao" do `ImmigrationManager.ExplainSuggestion`.
- Thông báo: `NotificationFeed.Show(msg, level, key, onClick)` — 3 mức Info/Warning/Critical, `onClick` dùng `UI/UIActions.cs` để tới nơi liên quan.
- Cài đặt `UI/SettingsPopup.cs` (mở từ Pause Menu, PlayerPrefs), nhạc/hiệu ứng `Controller/MusicManager.cs` (chưa có file âm thanh — gán clip trong Inspector), hướng dẫn `UI/TutorialHints.cs`.
- Khi chụp màn hình Play mode qua MCP mà Unity ở nền, chữ tiếng Việt có thể thiếu dấu do chưa vẽ lại — gọi `ForceMeshUpdate` trên các TMP_Text trước khi chụp, không phải lỗi font.
- Đổi format save → tăng `CitySaveSerializer.SaveVersion` và đọc có điều kiện theo version để save cũ vẫn mở được.
- Comment và text UI viết **tiếng Việt**.
- Có Unity MCP (`mcp__unityMCP__*`) để compile, đọc console, chạy Play mode, chụp màn hình.
