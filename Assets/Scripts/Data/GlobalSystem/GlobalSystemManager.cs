using UnityEngine;

public class GlobalSystemManager : MonoBehaviour
{
    [Header("Cấu Hình Thời Gian Mô Phỏng")]
    [Tooltip("Thời gian thực (giây) cho 1 tháng in-game")]
    [SerializeField] private float realSecondsPerMonth = 120f; // 2 phút thực = 1 tháng

    [Header("Bão tuyết (khủng hoảng chính MVP)")]
    [Tooltip("Xác suất bắt đầu bão tuyết ở mỗi tháng mùa đông khi trời đang quang")]
    [SerializeField, Range(0f, 1f)] private float blizzardChancePerWinterMonth = 0.4f;
    [Tooltip("Số tháng bão kéo dài (min, max)")]
    [SerializeField] private Vector2Int blizzardDurationMonths = new Vector2Int(1, 2);
    [SerializeField] private float blizzardTempDrop = -15f;
    [Tooltip("Than tiêu thụ thêm mỗi đầu người mỗi tháng khi có bão tuyết")]
    [SerializeField, Min(0)] private int blizzardExtraCoalPerCapita = 1;

    // Dữ liệu lõi toàn cầu (65 bytes)
    [SerializeField] private GlobalSystemData data;

    public float LastHungryRate { get; private set; }
    public float LastColdRate { get; private set; }
    public float LastThirstyRate { get; private set; }
    /// <summary>Sức cấp nước tháng gần nhất (số người): suối + Giếng nước.</summary>
    public int LastWaterCapacity { get; private set; } = WaterRules.BaseWaterCapacity;

    /// <summary>Sức cấp nước hiện tại (theo các Giếng đang hoạt động) — cho UI.</summary>
    public int CurrentWaterCapacity => WaterRules.GetCapacity(CollectBuildingEffects().wells);
    public float LastDiseasePressure { get; private set; }
    public BuildingEffectRules.MonthlyEffects LastBuildingEffects { get; private set; }

    [SerializeField] private BuildingManager buildingManager;

    /// <summary>Số giây thật cho 1 tháng ở tốc độ x1 (= 1 vòng ngày–đêm, xem DayCycle).</summary>
    public float RealSecondsPerMonth => realSecondsPerMonth;

    /// <summary>Hệ số tốc độ game hiện tại: 0 khi tạm dừng, 1/2/3 theo nút tốc độ.</summary>
    // GDD v0.3 mục 3: ở màn Cổng thời gian vẫn trôi nhưng chậm bằng 1/4 x1 (1 tháng = 8 phút), không dừng / tăng tốc được
    public const float GateSpeedMultiplier = 0.25f;
    public static bool GateViewOpen { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => GateViewOpen = false;

    /// <summary>Màn Cổng mở/đóng. Mở khi đang dừng thì cho thời gian chạy lại (ở Cổng không dừng được).</summary>
    public void SetGateView(bool open)
    {
        GateViewOpen = open;
        if (open && data.currentGameSpeed == GameSpeed.Pause
            && GamePlayController.Instance != null && GamePlayController.Instance.IsIn(GamePlayStateID.Playing))
            SetGameSpeed(GameSpeed.Speed1x);
    }

    public float SpeedMultiplier => data.currentGameSpeed == GameSpeed.Pause ? 0f
        : GateViewOpen ? GateSpeedMultiplier
        : data.currentGameSpeed switch
    {
        GameSpeed.Pause => 0f,
        GameSpeed.Speed2x => 2f,
        GameSpeed.Speed3x => 3f,
        _ => 1f
    };

    private void Update()
    {
        if (data.currentGameSpeed == GameSpeed.Pause) return;

        // Cập nhật tiến độ trôi qua của tháng hiện tại (0.0 -> 1.0)
        data.timeTickProgress += (Time.deltaTime * SpeedMultiplier) / realSecondsPerMonth;

        if (data.timeTickProgress >= 1.0f)
        {
            data.timeTickProgress -= 1.0f;
            AdvanceToNextMonth();
        }
    }

    // ==========================================
    // KHỞI TẠO VÀ LƯU/NẠP DỮ LIỆU
    // ==========================================

    public void InitializeNewGame()
    {
        data = new GlobalSystemData
        {
            currentYear = 1,
            currentMonth = 1,
            timeTickProgress = 0f,
            currentGameSpeed = GameSpeed.Speed1x,
            environmentTemperature = 20.0f,
            currentWeather = WeatherEvent.Clear,
            weatherDuration = 0,
            treasuryGold = 100,
            stockFood = 50,
            stockCoal = 30,
            stockWood = 80,
            stockIron = 20,
            stockMedicine = 10,
            authorityPoints = 100,
            reputation = StabilityRules.ReputationStart,
            entryTaxLevel = StabilityRules.DefaultEntryTax,
            pendingEntryTaxLevel = StabilityRules.DefaultEntryTax,
            wageLevel = WageLevel.Normal,
            pendingWageLevel = WageLevel.Normal,
            incomeTaxLevel = EntryTaxLevel.Normal,
            pendingIncomeTaxLevel = EntryTaxLevel.Normal,
            feedPolicy = FeedPolicy.Subsidize,
            foodPriceEvent = 1f,
            coalPriceEvent = 1f,
            woodPriceEvent = 1f,
            riotRiskMeter = 0.0f,
            totalPopulation = 0,
            totalInfected = 0,
            overallHappiness = 50.0f
        };

        SetGameSpeed(data.currentGameSpeed);
        PostMonthChanged(false);
        PostWeatherChanged();
        GameEvents.Post(EventID.ResourcesChanged);
    }

    // Trả về tham chiếu ref để SaveLoadManager đọc dữ liệu trực tiếp 0 byte GC
    public ref GlobalSystemData GetGlobalDataRef()
    {
        return ref data;
    }

    // Nạp dữ liệu từ Save Game vào
    public void SetGlobalData(in GlobalSystemData loadedData)
    {
        data = loadedData;

        // Cập nhật lại trạng thái thời gian và UI tức thì
        SetGameSpeed(data.currentGameSpeed);
        PostMonthChanged(false);
        PostWeatherChanged();
        GameEvents.Post(EventID.ResourcesChanged);
    }

    // ==========================================
    // LOGIC THỜI GIAN & THỜI TIẾT ĐỊNH KỲ
    // ==========================================

#if UNITY_EDITOR
    /// <summary>Test: tua ngay sang tháng sau (cùng đường xử lý với game thật).</summary>
    public void DebugAdvanceMonth() => AdvanceToNextMonth();
#endif

    private void AdvanceToNextMonth()
    {
        data.currentMonth++;
        if (data.currentMonth > 12)
        {
            data.currentMonth = 1;
            data.currentYear++;
            GameEvents.Post(EventID.YearChanged, data.currentYear);
        }

        // 1. Tính toán lại nhiệt độ môi trường theo Mùa
        UpdateSeasonalEnvironment();

        // GDD 6.6: giá thị trường (sự kiện, hồi giá, tin đồn) — trước khi hộ trả tiền ăn / sưởi
        MarketSystem.ProcessMonthly(ref data);

        // Chụp kho trước mô phỏng tháng để tính thay đổi ròng (sản xuất − tiêu hao) cho HUD
        ResourceAmounts before = ResourceAmounts.From(in data);

        // 2. Trừ tiêu hao tài nguyên & kiểm tra nguy cơ bạo loạn
        ProcessMacroMonthlyConsumption();

        // 3. Phát event thông báo sang tháng mới cho toàn bộ hệ thống
        //    (sản xuất, thuế, sắc lệnh, nghiên cứu, hàng chờ… đều chạy đồng bộ trong event này)
        PostMonthChanged(true);

        LastMonthNet = ResourceAmounts.From(in data) - before;
        GameEvents.Post(EventID.ResourcesChanged);
    }

    /// <summary>
    /// Thay đổi ròng của kho trong lần mô phỏng tháng gần nhất: mọi sản xuất, thuế trừ đi tiêu hao, sưởi,
    /// chi phí sắc lệnh / nghiên cứu… Không tính chi tiêu thủ công giữa hai tháng (xây, mua bán với thương nhân).
    /// Không lưu vào save: sau khi nạp game là 0 tới tháng kế tiếp.
    /// </summary>
    public ResourceAmounts LastMonthNet { get; private set; }

    private void UpdateSeasonalEnvironment()
    {
        // Điều chỉnh nhiệt độ cơ bản theo mùa
        float baseTemp = data.CurrentSeason switch
        {
            SeasonType.Spring => 18.0f,
            SeasonType.Summer => 32.0f,
            SeasonType.Autumn => 12.0f,
            SeasonType.Winter => -15.0f,
            _ => 20.0f
        };

        // Xử lý sự kiện thời tiết cực đoan. weatherDuration = số tháng còn lại, tính cả tháng hiện tại.
        if (data.weatherDuration > 0)
            data.weatherDuration--;
        if (data.currentWeather == WeatherEvent.Blizzard && data.CurrentSeason != SeasonType.Winter)
            data.weatherDuration = 0;   // Hết mùa đông thì bão tan
        if (data.weatherDuration == 0)
        {
            data.currentWeather = WeatherEvent.Clear;
            TryStartWeatherEvent();
        }
        ForecastNextBlizzard();

        if (data.currentWeather == WeatherEvent.Blizzard)
            baseTemp += blizzardTempDrop; // Rét buốt -30°C

        data.environmentTemperature = baseTemp;
        PostWeatherChanged();
    }

    /// <summary>
    /// GDD 2.2.i / 3.7: khủng hoảng chính Bão tuyết — chỉ xảy ra mùa đông, kéo dài vài tháng,
    /// làm lạnh thêm và đốt than nhiều hơn (dễ cạn kho than → chết rét → chuỗi khủng hoảng).
    /// </summary>
    private void TryStartWeatherEvent()
    {
        // GDD 9.2 + 6.6: bão tuyết luôn được báo trước 1 tháng — chỉ xảy ra khi tháng trước đã có dự báo
        bool forecast = data.blizzardForecast;
        data.blizzardForecast = false;
        if (data.CurrentSeason != SeasonType.Winter || !forecast)
            return;

        int minMonths = Mathf.Max(1, blizzardDurationMonths.x);
        int maxMonths = Mathf.Max(minMonths, blizzardDurationMonths.y);
        data.currentWeather = WeatherEvent.Blizzard;
        data.weatherDuration = (byte)Random.Range(minMonths, maxMonths + 1);
        // GDD 4.1: thiên tai sinh thêm đợt tị nạn, đông hơn 50% (ImmigrationManager xử lý rồi xoá cờ)
        data.disasterWaveBoost = true;
    }

    /// <summary>Tung xúc xắc bão tuyết cho tháng sau (nếu tháng sau là mùa đông và bão hiện tại không kéo sang) rồi báo trước.</summary>
    private void ForecastNextBlizzard()
    {
        if (data.blizzardForecast)
            return;
        int nextMonth = data.currentMonth % 12 + 1;
        bool nextWinter = nextMonth >= 10;
        bool stormContinues = data.currentWeather == WeatherEvent.Blizzard && data.weatherDuration > 1;
        if (!nextWinter || stormContinues)
            return;
        if (Random.value >= blizzardChancePerWinterMonth)
            return;
        data.blizzardForecast = true;
        MarketSystem.Announce(MarketEventType.Blizzard, MarketSystem.BlizzardMagnitude, true);
    }

    private BuildingEffectRules.MonthlyEffects CollectBuildingEffects()
    {
        if (buildingManager == null && GamePlayController.Instance != null && GamePlayController.Instance.playerContain != null)
            buildingManager = GamePlayController.Instance.playerContain.buildingManager;

        BuildingEffectRules.MonthlyEffects fx = buildingManager != null
            ? BuildingEffectRules.Collect(buildingManager.allBuildings, buildingManager.activeCount)
            : default;

        // Hiệu ứng thụ động từ cây nghiên cứu
        ResearchManager research = GamePlayController.Instance != null && GamePlayController.Instance.playerContain != null
            ? GamePlayController.Instance.playerContain.researchManager
            : null;
        if (research != null)
        {
            fx.gold += research.GoldPerMonth;
            fx.diseaseReduction += research.DiseaseReduction;
            fx.warmPeople += research.WarmPeople;
        }

        return fx;
    }

    private void ProcessMacroMonthlyConsumption()
    {
        // 0. Hiệu ứng thụ động của công trình (thuế CityHall, giếng, lửa trại, trang trí)
        BuildingEffectRules.MonthlyEffects fx = CollectBuildingEffects();
        LastBuildingEffects = fx;
        data.treasuryGold += fx.gold;
        data.authorityPoints = (ushort)Mathf.Min(ImmigrationRules.MaxAuthority,
            data.authorityPoints + ImmigrationRules.AuthorityRegenPerMonth);

        // GDD 6.4: đổi mức thuế nhập thành có hiệu lực từ tháng sau
        data.entryTaxLevel = data.pendingEntryTaxLevel;
        data.wageLevel = data.pendingWageLevel;
        data.incomeTaxLevel = data.pendingIncomeTaxLevel;

        // GDD 9.6: Reputation tự kéo về 50 một đoạn 10% chênh lệch, cộng ảnh hưởng thuế nhập thành
        // (Admit / Deny / người chết hay bỏ đi ngoài tường cộng trừ ngay khi xảy ra)
        data.reputation = StabilityRules.PullTowardStart(data.reputation);
        ModifyReputation(StabilityRules.GetEntryTaxReputation(data.entryTaxLevel));

        int foodPerCapita = GetFoodPerCapita(data.CurrentSeason);
        int needFood = data.totalPopulation * foodPerCapita;
        int consumedFood = Mathf.Min(data.stockFood, needFood);
        data.stockFood -= consumedFood;

        int peopleFed = foodPerCapita > 0 ? consumedFood / foodPerCapita : 0;
        int peopleHungry = Mathf.Max(0, data.totalPopulation - peopleFed);
        LastHungryRate = data.totalPopulation > 0
            ? (float)peopleHungry / data.totalPopulation
            : 0f;

        GameEvents.Post(EventID.HungerResolved, LastHungryRate);

        // Nước: Giếng nước cấp cho dân, thiếu thì một phần dân bị khát
        LastWaterCapacity = WaterRules.GetCapacity(fx.wells);
        LastThirstyRate = WaterRules.GetThirstyRate(data.totalPopulation, LastWaterCapacity);
        GameEvents.Post(EventID.ThirstResolved, LastThirstyRate);

        int coalPerCapita = data.CurrentSeason == SeasonType.Winter ? 1 : 0;
        if (data.currentWeather == WeatherEvent.Blizzard)
            coalPerCapita += blizzardExtraCoalPerCapita;
        int needCoal = data.totalPopulation * coalPerCapita;
        int consumedCoal = Mathf.Min(data.stockCoal, needCoal);
        data.stockCoal -= consumedCoal;

        // Lệnh khẩn cấp "Đốt gỗ sưởi ấm" (GDD 9.4): 2 gỗ thay 1 than cho phần thiếu, nhà kém ấm hơn 20%
        int woodWarm = 0;
        if (EmergencyOrders.BurnWoodActive && coalPerCapita > 0 && consumedCoal < needCoal)
        {
            int woodNeed = Mathf.CeilToInt((needCoal - consumedCoal) * EmergencyOrders.WoodPerCoal);
            int woodUsed = Mathf.Min(data.stockWood, woodNeed);
            data.stockWood -= woodUsed;
            woodWarm = Mathf.FloorToInt(woodUsed / EmergencyOrders.WoodPerCoal / coalPerCapita * EmergencyOrders.WoodHeatEfficiency);
        }

        int peopleWarm = coalPerCapita > 0
            ? Mathf.Min(data.totalPopulation, consumedCoal / coalPerCapita + woodWarm + fx.warmPeople)
            : data.totalPopulation;
        int peopleCold = Mathf.Max(0, data.totalPopulation - peopleWarm);
        LastColdRate = data.totalPopulation > 0
            ? (float)peopleCold / data.totalPopulation
            : 0f;
        GameEvents.Post(EventID.ColderResolved, LastColdRate);

        LastDiseasePressure = CalculateDiseasePressure();
        GameEvents.Post(EventID.DiseasePressure, LastDiseasePressure);
    }

    /// <summary>
    /// GDD v0.3 mục 7.4: không còn "áp lực dịch" chung — bệnh chỉ đến từ người bệnh (cổng, nhà, chỗ làm) và Miasma.
    /// Giá trị gửi đi = xác suất một cư dân nằm trong ít nhất một vùng Miasma (vùng không cộng dồn).
    /// </summary>
    private float CalculateDiseasePressure() => CrisisManager.GetMiasmaCoverage(in data);

    // ==========================================
    // CÁC HÀM TIỆN ÍCH TƯƠNG TÁC TÀI NGUYÊN (API)
    // ==========================================

    public void ModifyReputation(float amount)
    {
        data.reputation = Mathf.Clamp(
            data.reputation + amount,
            StabilityRules.ReputationMin,
            StabilityRules.ReputationMax);
        GameEvents.Post(EventID.ResourcesChanged);
    }

    /// <summary>Chọn mức thuế nhập thành — có hiệu lực từ tháng sau (GDD 6.4).</summary>
    public void SetPendingEntryTax(EntryTaxLevel level)
    {
        data.pendingEntryTaxLevel = level;
        GameEvents.Post(EventID.ResourcesChanged);
    }

    public void SetPendingWage(WageLevel level)
    {
        data.pendingWageLevel = level;
        GameEvents.Post(EventID.ResourcesChanged);
    }

    public void SetPendingIncomeTax(EntryTaxLevel level)
    {
        data.pendingIncomeTaxLevel = level;
        GameEvents.Post(EventID.ResourcesChanged);
    }

    /// <summary>Chính sách cấp phát / cắt khẩu phần — áp dụng ngay tháng tới khi tính tiền ăn của hộ.</summary>
    public void SetFeedPolicy(FeedPolicy policy)
    {
        data.feedPolicy = policy;
        GameEvents.Post(EventID.ResourcesChanged);
    }

    public BuildingEffectRules.MonthlyEffects CurrentBuildingEffects => CollectBuildingEffects();

    /// <summary>Lương thực / người / tháng theo mùa (mùa đông ăn gấp đôi).</summary>
    public static int GetFoodPerCapita(SeasonType season) => season == SeasonType.Winter ? 2 : 1;

    public void ModifyGold(int amount)
    {
        data.treasuryGold = Mathf.Max(0, data.treasuryGold + amount);
        GameEvents.Post(EventID.ResourcesChanged);
    }

    public void ConsumeAvailable(ResourceType type, int amount)
    {
        if (amount <= 0)
            return;

        switch (type)
        {
            case ResourceType.Food:
                data.stockFood = Mathf.Max(0, data.stockFood - amount);
                break;
            case ResourceType.Coal:
                data.stockCoal = Mathf.Max(0, data.stockCoal - amount);
                break;
            case ResourceType.Wood:
                data.stockWood = Mathf.Max(0, data.stockWood - amount);
                break;
            case ResourceType.Iron:
                data.stockIron = Mathf.Max(0, data.stockIron - amount);
                break;
            case ResourceType.Medicine:
                data.stockMedicine = Mathf.Max(0, data.stockMedicine - amount);
                break;
            default:
                return;
        }

        GameEvents.Post(EventID.ResourcesChanged);
    }

    public bool TryConsumeFood(int amount)
    {
        if (amount <= 0)
            return true;

        if (data.stockFood < amount)
            return false;

        data.stockFood -= amount;
        GameEvents.Post(EventID.ResourcesChanged);
        return true;
    }

    public bool ConsumeCoal(int amount)
    {
        if (amount <= 0)
            return true;

        if (data.stockCoal < amount)
            return false;

        data.stockCoal -= amount;
        GameEvents.Post(EventID.ResourcesChanged);
        return true;
    }

    public bool TryConsumeMedicine(int amount)
    {
        if (amount <= 0)
            return true;

        if (data.stockMedicine < amount)
            return false;

        data.stockMedicine -= amount;
        GameEvents.Post(EventID.ResourcesChanged);
        return true;
    }

    public void AddResource(ResourceType type, int amount)
    {
        switch (type)
        {
            case ResourceType.Food: data.stockFood += amount; break;
            case ResourceType.Coal: data.stockCoal += amount; break;
            case ResourceType.Wood: data.stockWood += amount; break;
            case ResourceType.Iron: data.stockIron += amount; break;
            case ResourceType.Medicine: data.stockMedicine += amount; break;
        }
        GameEvents.Post(EventID.ResourcesChanged);
    }

    private void PostMonthChanged(bool isSimulationTick)
    {
        GameEvents.Post(EventID.MonthChanged, new MonthChangedPayload
        {
            year = data.currentYear,
            month = data.currentMonth,
            season = data.CurrentSeason,
            isSimulationTick = isSimulationTick
        });
    }

    private void PostWeatherChanged()
    {
        GameEvents.Post(EventID.OnWeatherChanged, new WeatherChangedPayload
        {
            weather = data.currentWeather,
            temperature = data.environmentTemperature
        });
    }

    public void SetGameSpeed(GameSpeed speed)
    {
        data.currentGameSpeed = speed;
        Time.timeScale = speed == GameSpeed.Pause ? 0f : 1f;
    }

    // Cập nhật bộ đệm để HUD hiển thị nhanh không cần quét mảng
    public void UpdateMetricsCache(int popCount, int infectedCount, float avgHappiness)
    {
        data.totalPopulation = popCount;
        data.totalInfected = infectedCount;
        data.overallHappiness = avgHappiness;
    }
}

/// <summary>Số lượng 6 loại tài nguyên trong kho (dùng cho chênh lệch theo tháng).</summary>
public struct ResourceAmounts
{
    public int gold, food, coal, wood, iron, medicine;

    public static ResourceAmounts From(in GlobalSystemData d) => new ResourceAmounts
    {
        gold = d.treasuryGold,
        food = d.stockFood,
        coal = d.stockCoal,
        wood = d.stockWood,
        iron = d.stockIron,
        medicine = d.stockMedicine
    };

    public static ResourceAmounts operator -(ResourceAmounts a, ResourceAmounts b) => new ResourceAmounts
    {
        gold = a.gold - b.gold,
        food = a.food - b.food,
        coal = a.coal - b.coal,
        wood = a.wood - b.wood,
        iron = a.iron - b.iron,
        medicine = a.medicine - b.medicine
    };
}
