 Icon Game   
Project: ***Working Title***  
Genre: City-builder / Management / Border Inspection  
Platform: PC  
Distribution: Steam (planned)   
Engine: Unity 2022.3.62f2  
Document Status: Draft / Working GDD  
Current Version: v0.2 

| Version | Ngày cập nhật | Người thực hiện | Nội dung cập nhật |
| :---: | :---: | :---: | ----- |
| v0.1 | 23/08/2026 | Lê Quốc Anh | Khởi tạo khung GDD theo ý tưởng thành chủ, kiểm dịch và quy hoạch |
| v0.2 | 16/09/2026 | Lê Quốc Anh | Tinh gọn và tái cấu trúc GDD; làm rõ MVP, Core Mechanics, Progression và Future Scope. |

# **1\. GAME OVERVIEW**

## **1.1. High Concept:**

	Người chơi vào vai Thành Chủ của một thành bang biên giới đang suy tàn, chịu trách nhiệm kiểm soát dòng người tị nạn, xây dựng và vận hành thành phố, phân bổ lao động, quản lý tài nguyên và ban hành các sắc lệnh nhằm giúp lãnh địa tồn tại và phát triển qua dịch bệnh, thiên tai và áp lực theo mùa.  
Điểm đặc trưng của game là kết hợp kiểm duyệt người nhập cư trực tiếp tại Cổng Thành với city-builder / management simulation, trong đó những quyết định tại cổng có ảnh hưởng trực tiếp đến dân số, kinh tế, dịch bệnh và sự ổn định của thành phố.

## **1.2. Genre:** 

Management Simulation / City Builder / Border Inspection 

## **1.3. Platform** 

Target Platform: PC  
Primary Store: Steam (planned)  
Input: Keyboard \+ Mouse 

## **1.4. Target Audience**

Người chơi yêu thích game quản lý hệ thống, xây dựng thành phố và đưa ra các quyết định có hậu quả dài hạn.  
Primary Audience

* Người chơi PC: Initial Assumption.  
* Thích Management / Simulation / Strategy.  
* Thích tối ưu tài nguyên và xây dựng hệ thống tự vận hành.  
* Chấp nhận mức độ phức tạp trung bình–cao.

Player Motivation

* Xây dựng một thành phố từ yếu → mạnh.  
* Tối ưu hệ thống kinh tế và lao động.  
* Giải quyết khủng hoảng.  
* Tạo ra bộ luật/sắc lệnh tự động hóa thành phố.  
* Quan sát hậu quả của những quyết định mình đưa ra.

## **1.5. Reference Games**

**Papers, Please:** Tham khảo trải nghiệm kiểm tra hồ sơ, đưa ra phán quyết và áp lực xử lý từng người.  
**Frostpunk:** Tham khảo city survival, quản lý mùa đông, tài nguyên và các quyết định mang tính đánh đổi.  
**Banished:** Tham khảo population simulation, lao động, sản xuất và chuỗi cung ứng.  
**Oxygen Not Included:** Tham khảo cách hiển thị nhiều hệ thống quản lý và thông tin lên HUD.

## **1.6. Core Gameplay Loop**

Kiểm tra người đến Cổng → Admit / Deny / Quarantine → Tăng hoặc giảm dân số / nhân lực / rủi ro dịch bệnh → Phân bổ nhà ở và công việc → Sản xuất tài nguyên và phát triển thành phố → Đối phó thiếu hụt, mùa đông, dịch bệnh và biến cố → Nghiên cứu công nghệ / ban hành sắc lệnh / tự động hóa → Thu hút làn sóng dân cư lớn hơn và khó xử lý hơn → Quay lại Cổng Thành

## **1.7. Design Pillars**

**Pillar 1 — Every Decision Has a Cost**

* Không có lựa chọn hoàn toàn đúng.  
* Cho người vào tăng nhân lực nhưng tăng áp lực lương thực, nhà ở và nguy cơ dịch bệnh.  
* Từ chối giúp thành phố an toàn hơn nhưng có thể mất lao động và Uy tín.  
  **Pillar 2 — Systems Create Consequences**  
* Hệ thống phải liên kết với nhau và tạo phản ứng dây chuyền.  
* Thiếu than → chết cóng → xác không xử lý → Miasma → dịch → Riot.

**Pillar 3 — Manual to Automation**

* Người chơi ban đầu phải xử lý mọi thứ bằng tay.  
* Manual Inspection → Rules → Edicts → Automation

**Pillar 4 — Growth Creates Pressure**

* Thành phố càng mạnh thì thử thách càng lớn.  
* Công nghệ cao hơn mở dòng di cư lớn hơn, bệnh phức tạp hơn và các biến cố nguy hiểm hơn.

  # **2\. SCOPE / MVP / FUTURE SCOPE**

## **2.1. Development Goal** 

Mục tiêu của giai đoạn đầu là xây dựng một Vertical Slice chứng minh Core Gameplay Loop hoạt động:  
Kiểm duyệt dân tại Cổng → đưa dân vào thành → phân bổ nhà/việc → sản xuất tài nguyên → đối mặt với khủng hoảng → nâng cấp khả năng quản lý.

## **2.2. MVP** 

**a) Gate Inspection**   
NPC đến Cổng Thành.  
Hồ sơ cơ bản: tên, nghề nghiệp, nguồn gốc.  
Kiểm tra triệu chứng cơ bản.  
3 quyết định:

* Admit  
* Deny  
* Quarantine  
  Waiting Yard.  
  1 loại bệnh chính: Sốt Đỏ.  
  Quyết định tại cổng tác động tới Population / Reputation / Disease Risk.

**b) City Building**   
Building MVP

* Grid placement.  
* Road.  
* House.  
* Food production.  
* Coal production.  
* Storage.  
* Quarantine Station.  
* Medical building cơ bản.

Kích thước: 1x1 và 2x2 và 3x3.  
**c) Population Management**   
Mỗi cư dân MVP có:

* Profession  
* Wealth  
* Health State  
* Symptoms

Thành phố theo dõi:

* Total Population  
* Homeless  
* Unemployed  
* Sick  
* Happiness

**d) Economy & Resources** 

* Food: Dùng để nuôi dân.  
* Coal: Dùng để sưởi mùa Đông.  
* Wood: Dùng để xây dựng.  
* Gold: Dùng để xây, thương mại và duy trì hệ thống.  
* Medicine: Chữa bệnh cho dân.

**e) Season System**   
Hệ thống có 4 mùa: Xuân, Hạ, Thu, Đông  
Xuân: tăng sản lượng food  
Winter:

* Farm giảm/ngừng sản xuất.  
* Coal consumption tăng.  
* Nhà không được sưởi → người dân có nguy cơ chết.

**f) Disease System**

* Chỉ 1 Disease: Sốt Đỏ.  
* NPC có thể: Healthy hoặc Infected  
* Người nhiễm lọt vào thành → có khả năng lây bệnh cho cư dân.  
* Quarantine → phát hiện / xử lý ca bệnh.

**g) Edict / Automation System**   
Maximum 3 rules.  
Condition:

* Profession  
* Disease symptom  
* Origin  
  Operator:  
* IF  
* AND  
  Action:  
* Admit  
* Deny  
* Quarantine

Ví dụ:  
IF \[Temperature \> 38.5\]  
THEN \[QUARANTINE\]  
**h) Technology / Progression** 

* MVP không cần full Tech Tree.  
* Chỉ cần: Tier 0 → Tier 1  
* Ví dụ: Tier 0 kiểm duyệt thủ công. → Tier 1 mở Thermometer, mở 3 Edict Lines, mở House upgrade / Heating cơ bản.

**i) Crisis / Disaster**   
MVP cần: 

* Primary Crisis: Winter / Snowstorm  
* Secondary Risk: Disease Outbreak

**k) Reputation / Stability**   
Reputation: Đại diện cho cách thế giới bên ngoài nhìn thành phố. → Ảnh hưởng: loại / số lượng người tìm đến Cổng.  
Riot Meter: Đại diện cho bất ổn nội thành. → Ảnh hưởng: 100% \= Lose.  
**l) Trade**   
Thỉnh thoảng một thương nhân đến Cổng.  
Player có thể:

* Buy / Sell:  
* Food  
* Coal  
* Wood

**m) MVP Win / Lose**   
Win: Chuẩn bị thành phố để sống sót mùa Đông đầu tiên.

* Survive the first Winter.  
* Có thể thêm điều kiện:  
* Population ≥ 20\.

Lose: Population \= 0 hoặc Riot Meter \= 100%

## **2.3. Future Scope / Out of MVP**

Phase 1 — Full Core Game

* Mô phỏng đầy đủ vòng đời dân số  
* Hệ thống nhiều loại bệnh  
* Mở rộng cây công nghệ  
* Mô phỏng đầy đủ chuỗi cung ứng  
* Hệ thống sắc lệnh OR / ELSE  
* Mở rộng hệ thống thương mại  
* Bổ sung thêm nhiều loại thảm họa  
* Xây dựng thành phố với quy mô lớn hơn

Phase 2

* Hệ thống phe phái hoàn chỉnh  
* Sự kiện cốt truyện qua radio  
* Phe phái tôn giáo  
* Hệ thống thám hiểm  
* Chiến dịch quân sự  
* Xây dựng và quản lý tiền đồn  
* Sáp nhập lãnh thổ  
* World Map chiến lược quy mô lớn  
* Ngoại giao giữa nhiều thành bang  
* Tiến trình khoa học giai đoạn cuối game  
* Nhiều điều kiện chiến thắng

  # **3\. Core Mechanics**

## **3.0. Canonical Time System**

1 Year \= 12 Months  
1 Season \= 3 Months  
1 Month \= đơn vị simulation chính  
Các hệ thống tính theo Month:

* Consumption  
* Production  
* Tax  
* Quarantine  
* Disease progression  
* Research  
* Trade travel

Gate Inspection diễn ra realtime trong từng ca làm việc nhưng không sử dụng một timeline độc lập làm dân già đi.

## **3.1. Gate Inspection**

1) **NPC Arrival** 

NPC xuất hiện tại Waiting Yard và lần lượt được đưa tới bàn kiểm duyệt.  
Mỗi NPC có tập thuộc tính:

* Profession  
* Origin  
* Wealth  
* Health State  
* Symptoms  
* Documents  
* Special Tags (nếu có)  
2) **Inspection**

Người chơi kiểm tra NPC thông qua:  
Hồ sơ → triệu chứng → so sánh với biểu mẫu → quyết định.

3) **Verdict**   
* ADMIT: Cho NPC trở thành cư dân.  
* DENY: Từ chối NPC và loại khỏi hàng đợi.  
* QUARANTINE: Chuyển NPC sang Trạm Cách Ly để theo dõi.  
4) **Admit** 

Khi Admit: 

* NPC → Citizen  
* Population \+1 → kiểm tra Housing → kiểm tra Job → bắt đầu tiêu thụ resource.

Nếu không có nhà: Status \= Homeless  
Homeless làm tăng:

* Unhappiness  
* Disease Risk  
* Winter Mortality Risk.  
5) **Deny**

NPC rời Waiting Yard.  
Quyết định có thể ảnh hưởng Reputation tùy:

* Health  
* Profession  
* Origin  
* Special status  
* Current crisis  
6) **Quarantine**

NPC được chuyển tới Quarantine Station.  
Quarantine có:

* Capacity  
* Food Consumption  
* Heating Consumption  
* Medical Staff Requirement  
* Trong quá trình cách ly:  
* Healthy → được nhập thành.  
* Infected → Treatment / Isolation / Death.

Default Quarantine Duration: 3 Months — Balance TBD

7) **Waiting Yard**

NPC chưa được xử lý nằm trong Waiting Yard.  
Waiting Yard có Capacity.  
Hàng đợi quá dài có thể gây:

* Hunger  
* Disease transmission  
* Unrest  
* Gate pressure.

## **3.2. Population System**

Citizen State: Mỗi Citizen cần tối thiểu:

* Profession  
* Housing  
* Employment  
* Health  
* Happiness

Population Flow  
Gate→ Citizen → Housing → Job → Production → Consumption → Happiness / Health.

1) **Housing** 

Mỗi building dân cư có Capacity. Nếu Population \> Housing Capacity: → Homeless tăng. → Homeless tạo áp lực xã hội và sinh tồn.  
Admit quá nhiều người → city chịu hậu quả.

2) **Employment**

Công trình sản xuất có Worker Slots: Citizen có profession phù hợp được ưu tiên gắn vào công việc tương ứng.  
Ví dụ:

* Farmer → Farm  
* Doctor → Clinic  
* Scholar → Research  
* Miner → Mine.

Nếu không có việc: Status \= Unemployed.  
Sau khi mở khóa Edict / Automation có thể tự động phân bổ.

3) **Lifecycle**

Child → Adult → Elderly → Death 

## **3.3. City Building** 

Thành phố sử dụng square grid.  
Buildings có footprint:1×1, 2×2 và 3×3.

1) **Road Connectivity** 

Building phải có kết nối logistics tới Road Network / Storage để hoạt động.  
Flow:Producer→ Road→ Storage→ Consumer.

2) **Logistic**

Porters vận chuyển resource giữa Storage và Buildings.  
Thời gian vận chuyển phụ thuộc:

* Distance  
* Road quality  
* Porter availability  
* Carry capacity.  
3) **Resources & Economy** 

Basic Resources

* Food  
* Wood  
* Coal  
* Gold  
* Medicine

Advanced Resources Phase sau:

* Stone  
* Iron  
* Glass  
* Oil  
* Research Materials.  
4) **Production** 

Rule chung: Building \+ Worker \+ Input \+ Time → Output.  
Ví dụ: Farm \+ Farmer → Food  
hoặc:  
Herbal Garden → Herbs → Medicine Workshop → Medicine.

5) **Consumption** 

Mỗi Month:

* Dân sẽ tiêu thụ Food.  
* Sưởi ấm nhà tiêu thụ Coal trong Mùa Đông.  
* Xây nhà tiêu thụ tài nguyên.

Nếu thiếu: 

* Food shortage → Hunger → Health giảm → Happiness giảm.  
* Coal shortage in Winter→ Temperature giảm → Sickness / Death.

## **3.4. Disease System**

Health States

* Healthy  
* Incubating  
* Infected  
* Recovering  
* Dead  
1) **Infection** 

Nguồn bệnh chính:

* Infected migrant được Admit  
* Cross-infection trong Waiting Yard / Quarantine.

Sau khi vào thành: infected citizen → nearby population → outbreak.

2) **Treatment** 

Clinic / Hospital cần:

* Doctor  
* Medicine  
* Capacity

Nếu đủ: Recovery chance tăng.  
Nếu quá tải: mortality tăng, disease spread tăng.

## **3.5. Edict & Automation System**

Basic Structure

* IF \[Condition\]  
* AND / OR \[Condition\]  
* THEN \[Action\]  
1) **Example** 

IF \[Temperature \> 38.5\]  
THEN \[QUARANTINE\]  
hoặc:  
IF \[Profession \= Doctor\]  
AND \[Healthy\]  
THEN \[ADMIT\]

2) **Automation Progression** 

Tier 0: Manual.  
Tier 1: IF \+ AND.  
Tier 2: OR \+ ELSE \+ resource conditions.  
Tier 3: Nested rules / broad automation.

3) **Conflict Resolution** 

Yêu cầu:

* Top-to-bottom Priority  
* System kiểm tra rules từ trên xuống.  
* Rule đầu tiên match hoàn chỉnh được thực thi.  
* Các rule bên dưới không chạy.

Và UI phải có: “Why was this NPC processed?”

## **3.6. Seasons & Environmental Pressure**

Full Game có:

* Spring  
* Summer  
* Autumn  
* Winter.

Mỗi Season thay đổi production, consumption và disease pressure.  
Ví dụ:

* Spring: migration tăng, farming starts.  
* Summer: agricultural production tăng.  
* Autumn: harvest / preparation.  
* Winter: agriculture giảm, heating consumption tăng, mortality risk tăng.

## **3.7. Crisis Chain** 

* Giai đoạn 1 (Nguyên nhân): Bão tuyết kéo dài làm cạn kiệt kho Than dự trữ.  
* Giai đoạn 2 (Hậu quả trực tiếp): Cư dân trong các Lều tạm 1x1 bắt đầu chết cóng vì không có nhiệt độ sưởi ấm.  
* Giai đoạn 3 (Biến cố thứ cấp): Đội thu gom thi thể không làm việc kịp → Xác chết phân hủy sau 1 tháng tạo ra khí độc Miasma → Dịch bệnh bùng phát ngay giữa mùa đông.  
* Giai đoạn 4 (Vỡ trận): Bệnh nhân tràn ngập làm sụp đổ Bệnh Xá →  Thanh Phẫn Nộ (Riot Meter) chạm 100% dẫn đến bạo loạn lật đổ chính quyền.

## **3.8. Research Mechanic**

Research Building

* Scholar / Doctor  
* Resource Cost  
* Time→ Research Points→ Unlock Technology.

## **3.9. Trade** 

Phase đầu:  
Thương nhân đến → player buys / sells resource.  
Full:  
Bản đồ thế giới → chọn khu định cư → tạo tuyến đường giao thương → phân bổ hàng hóa → chỉ định đội hộ tống → khởi hành → gặp sự kiện / nguy cơ bị cướp → trở về.

# **4\. Progression** 

## **4.1. Progression Goal**

Progression của game thể hiện quá trình người chơi phát triển từ một thành trì biên giới nhỏ, thiếu nhân lực và công nghệ thành một thành bang tự vận hành, sau đó trở thành một thế lực có khả năng mở rộng lãnh thổ hoặc phát triển khoa học tới cấp độ không gian.  
Mỗi bước tiến không chỉ làm người chơi mạnh hơn mà đồng thời mở ra:

* Quy mô dân số lớn hơn.  
* Chuỗi sản xuất phức tạp hơn.  
* Các loại bệnh và khủng hoảng nguy hiểm hơn.  
* Nhu cầu quản trị cao hơn.  
* Các lựa chọn chiến lược mới.

Progression phải tuân theo nguyên tắc: **Growth Creates Pressure.**

## **4.2. Main Progression Layers**

1)  **Population Progression**

Dân số nhỏ → khu định cư → thành phố → thành bang → trung tâm khu vực → nền văn minh lớn.  
Population càng cao:

* Workforce tăng  
* Tax tăng  
* Army potential tăng

nhưng đồng thời:

* Food demand tăng  
* Housing demand tăng  
* Disease risk tăng  
* Administration complexity tăng.  
2) **City Progression**

Lều / đường đất → nhà gỗ → khu sản xuất → hạ tầng hơi nước → thành phố công nghiệp → thành phố hiện đại.

3) **Administrative Progression**

Quản lý thủ công → Sắc lệnh đơn giản → Quản lý theo điều kiện → Tự động hóa hành chính → Quản trị quy mô lớn  
Ví dụ:

* Early game:  Player tự kiểm từng NPC.  
* Mid game: IF / AND xử lý các trường hợp rõ ràng.  
* Late game: Hệ thống tự giải quyết phần lớn flow, player chỉ xử lý exception.  
4) **Technology Progression**

Công nghệ mở:

* Buildings  
* Production chains  
* Medicine  
* Automation  
* Military  
* Energy  
* Transportation  
* Space Program.  
5) **Territorial Progression**

Early game: chỉ quản lý thành phố.  
Mid game: giao thương với vùng khác.  
Late-mid game: Outposts / military expeditions.  
Late game: Conquest / Annexation.  
Endgame: kiểm soát toàn bộ bản đồ hoặc tập trung nguồn lực vào Space Program.

## **4.3. Era Progression**

**Era I — Survival:** Thành trì nhỏ, tài nguyên hạn chế.  
Gameplay chính:

* Duyệt dân thủ công.  
* Food / Wood / Coal.  
* Housing.  
* Basic disease.  
* Survive Winter.  
* Basic Research.

Player fantasy: “Làm sao để thành này không chết?”  
**Era II — Administration:** Thành bắt đầu phát triển.  
Mở:

* Town Hall.  
* Edict System.  
* Advanced quarantine.  
* Expanded production.  
* Stone / Iron.  
* Trade.  
* Larger migration waves.

Player fantasy: “Làm sao quản lý được ngày càng nhiều người?”  
**Era III — Industrialization** Bước ngoặt lớn.  
Mở:

* Steam power.  
* Factory.  
* Advanced metallurgy.  
* Railway / improved logistics.  
* Large housing.  
* Mass production.  
* Advanced medicine.

Administration: automation bắt đầu thay thế manual work.  
Player fantasy: “Làm sao biến thành phố thành một cỗ máy?”  
**Era IV — Regional Power:** Lúc này game bắt đầu mở rộng ra khỏi city.  
Mở:

* World Map sâu hơn.  
* Outposts.  
* Standing Army.  
* Military logistics.  
* Diplomacy.  
* Territorial conquest.  
* Electricity.  
* Advanced science.

Player bắt đầu có lựa chọn chiến lược: Expansion hoặc Science.  
**Era V — Civilization / Endgame:** Đây là thời kỳ quyết định Victory Path.

* Conquest Path Quân đội → chiến tranh → chiếm vùng → sáp nhập → kiểm soát toàn bộ bản đồ.  
* Science Path Công nghiệp hóa → năng lượng tiên tiến → máy tính → công nghệ tên lửa → chương trình không gian → phóng tàu.

## **4.4. Tech Tree Structure**

A. Y tế & Kiểm dịch

* Detection  
* Quarantine  
* Treatment  
* Vaccination  
* Advanced Medicine.

B. Hạ tầng & Năng lượng

* Roads  
* Heating  
* Steam  
* Electricity  
* Advanced Energy.

C. Nông nghiệp & Công nghiệp

* Farming  
* Storage  
* Manufacturing  
* Mass Production  
* Advanced Materials.

D. Luật pháp & Quản trị

* Edicts  
* Taxes  
* Automation  
* Bureaucracy  
* Large-scale Administration.

E. Quân sự & Mở rộng

* City Defense  
* Weapons  
* Standing Army  
* Military Logistics  
* Expedition  
* Conquest.

F. Khoa học & Không gian  
Research Method → Advanced Science → Computing → Rocketry → Space Program.

# **5\. GAME FLOW**

## **5.1. High-Level Game Flow**

Home Screen → New Game / Continue → Loading → City View → Gate / Building / Population / Research / Trade / Edict → Monthly Simulation Update → Season / Crisis / Migration Update → Progression & Expansion → Victory Condition  
Tóm lại toàn bộ game chạy theo vòng:  
Tiếp nhận dân → Phát triển thành phố → Tạo tài nguyên → Nghiên cứu / mở rộng → Đối mặt áp lực mới → Tiếp tục phát triển

## **5.2. Main Gameplay States**

**A. City View:** Đây là màn hình quản lý chính.  
Người chơi thực hiện:

* Xây dựng công trình.  
* Phân bổ lao động.  
* Theo dõi tài nguyên.  
* Quản lý dân cư.  
* Xử lý khủng hoảng.  
* Kiểm tra Happiness / Riot / Reputation.  
* Truy cập các hệ thống khác.

**B. Gate Inspection**  
Từ City View: City View → Gate  
Người chơi chuyển tới bàn kiểm duyệt để xử lý người đang chờ.  
Flow:NPC tới bàn → Kiểm tra hồ sơ → Kiểm tra triệu chứng / công cụ → Quyết định:Admit / Deny / Quarantine → Cập nhật Population / Reputation / Waiting Yard → NPC tiếp theo.  
Khi rời Gate: Gate → City View  
Những NPC được Admit sẽ trở thành Citizen và bắt đầu tác động tới thành phố.

## **5.3. MVP Flow**

New Game → City nhỏ ban đầu → Nhận nhóm Migrants đầu tiên → Admit / Deny / Quarantine → Xây Housing → Xây Food Production → Xây Coal Production → Population tăng → Mở Edict cơ bản → Chuẩn bị Winter → Winter Crisis → Survive → MVP Complete

# **6\. Control**

| Input | Chức năng |
| ----- | ----- |
| WASD | Pan bản đồ |
| Giữ Middle Mouse \+ Drag | Pan bản đồ |
| Mouse Wheel | Phóng to / thu nhỏ |
| Left Click | Chọn Citizen / Building / Resource / UI |
| Right Click | Hủy thao tác hiện tại |
| Esc | Đóng cửa sổ / mở Pause Menu |
| Space | Dừng / tiếp tục simulation |
| Tab | x1 / x2 / x3 |
| R | Xoay công trình khi đang đặt |
| Left Click | Đặt công trình |
| Right Click / Esc | Hủy placement |
| Delete | Chế độ phá công trình |
| F5 | Quick Save |
| N | News / Radio  |
| G | Cổng thành |
| H | Trung tâm thành phố |
| T | Tech Tree |
| E | Edicts Rule |
| M | World Map |
| 1/2/3/4 | Xây các nhà thuộc thể loại: Hạ tầng năng lượng/ Y tế/ Sản xuất/ Quân sự |

## **6.1. Camera Controls**

City View: Camera ¾ Isometric / Top-down, cho phép pan và zoom.  
Gate Inspection: Fixed Camera.  
Trade / World Map: Top-down map camera.  
Camera Limits

* Camera không được di chuyển ra quá xa khỏi vùng lãnh thổ người chơi sở hữu.  
* Zoom Min giữ đủ gần để kiểm tra Citizen/Building.  
* Zoom Max cho phép quan sát toàn bộ khu vực thành phố hiện tại.  
* Các vật thể che camera có thể sử dụng Fade / Dither Transparency.

## **6.2. Gate Inspection Controls**

| Hành động | Input |
| ----- | ----- |
| Chọn hồ sơ / NPC | Left Click |
| Admit | Click dấu Admit |
| Deny | Click dấu Deny |
| Quarantine | Click dấu Quarantine |
| NPC tiếp theo | Tự động sau khi phán quyết |
| Pause | Space |
| Rời Gate | G hoặc nút UI |

## **6.3. Building Placement Controls**

Flow:  
Chọn Building→ Building Ghost xuất hiện→ Di chuyển chuột→ Grid Snap→ Validate Placement→ Left Click xác nhận.  
Trạng thái preview:

* Valid → có thể xây.  
* Invalid → không thể xây.

Các nguyên nhân invalid:

* Collision  
* Outside owned territory  
* Invalid terrain  
* Missing required connection.

## **6.4. World Map Controls** 

| Hành động | Input |
| ----- | ----- |
| Pan Map | WASD / Middle Mouse Drag |
| Zoom | Mouse Wheel |
| Chọn Territory / City | Left Click |
| Chọn Route | Left Click |
| Hủy | Right Click / Esc |
| Focus Player Territory | Home |

## **6.5. Military Controls — Future Scope** 

Player không điều khiển trực tiếp từng Soldier mà sẽ điều khiển 1 đội quân hùng hậu.  
Flow:  
Chọn Army → Chọn Target Territory → Chọn Mission: Defense / Expedition / Conquest → Confirm.

# **7\. UI / UX** 

## **7.1. HUD Layout**

Top Left: Thời gian, trạng thái Pause/Speed, Dân số, Dịch bệnh, Ổn định xã hội, Uy tín và các cảnh báo chính.  
Top Right: Danh sách tài nguyên có thể Pin/Unpin để theo dõi. Mặc định có thể ghim Lương thực, Gỗ, Than, Vàng.  
Bottom Bar: Menu xây dựng, chia theo nhóm như Hạ tầng, Y tế, Sản xuất, Quân sự.  
Time Control: Space \= Pause/Resume, Tab \= chuyển x1 → x2 → x3.  
Reference: Oxygen Not Included — tham khảo cách tổ chức HUD, resource tracking, alert system và information hierarchy.

## **7.2. Selection & Context Panel**

**Building**  
Hiển thị:

* Status  
* Workers  
* Input / Output  
* Production Rate  
* Efficiency  
* Storage  
* Upgrade / Enable / Disable / Demolish

Nếu building không hoạt động: UI phải hiển thị trực tiếp nguyên nhân.  
Ví dụ: Not Working — No Workers hoặc: Not Working — Missing Coal  
**Citizen**  
Hiển thị:

* Name  
* Profession  
* Health  
* Home  
* Workplace  
* Happiness  
* Current Status

Ví dụ: Homeless, Unemployed, Sick, Quarantined.

## **7.3. Alerts**

Alert được chia theo mức độ.

* Info  
  Ví dụ: Research Complete, Merchant Arrived.  
* Warning

Ví dụ:, Low Food, Low Coal, Homeless, Building Missing Workers.

* Critical

Ví dụ: Disease Outbreak, Riot Critical, Enemy Attack.  
Click Alert: Camera / UI chuyển trực tiếp tới vị trí hoặc đối tượng liên quan.  
Ví dụ: 5 Homeless → click → mở danh sách Citizen.

