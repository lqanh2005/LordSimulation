using UnityEngine;

public class GlobalSystemManager : MonoBehaviour
{
    [Header("Cấu Hình Thời Gian Mô Phỏng")]
    [Tooltip("Thời gian thực (giây) cho 1 tháng in-game")]
    [SerializeField] private float realSecondsPerMonth = 120f; // 2 phút thực = 1 tháng

    // Dữ liệu lõi toàn cầu (65 bytes)
    [SerializeField] private GlobalSystemData data;

    public float LastHungryRate { get; private set; }
    public float LastColdRate { get; private set; }
    public float LastDiseasePressure { get; private set; }

    private void Update()
    {
        if (data.currentGameSpeed == GameSpeed.Pause) return;

        // Tính tốc độ mô phỏng (1x, 2x, 3x)
        float speedMultiplier = data.currentGameSpeed switch
        {
            GameSpeed.Speed1x => 1f,
            GameSpeed.Speed2x => 2f,
            GameSpeed.Speed3x => 3f,
            _ => 1f
        };

        // Cập nhật tiến độ trôi qua của tháng hiện tại (0.0 -> 1.0)
        data.timeTickProgress += (Time.deltaTime * speedMultiplier) / realSecondsPerMonth;

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
            stockWood = 40,
            stockIron = 20,
            stockMedicine = 10,
            authorityPoints = 100,
            reputation = 0.0f,
            riotRiskMeter = 0.0f,
            totalPopulation = 0,
            totalInfected = 0,
            overallHappiness = 50.0f
        };
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
        PostMonthChanged();
        PostWeatherChanged();
        GameEvents.Post(EventID.ResourcesChanged);
    }

    // ==========================================
    // LOGIC THỜI GIAN & THỜI TIẾT ĐỊNH KỲ
    // ==========================================

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

        // 2. Trừ tiêu hao tài nguyên & kiểm tra nguy cơ bạo loạn
        ProcessMacroMonthlyConsumption();

        // 3. Phát event thông báo sang tháng mới cho toàn bộ hệ thống
        PostMonthChanged();
        GameEvents.Post(EventID.ResourcesChanged);
    }

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

        // Xử lý sự kiện thời tiết cực đoan (Bão tuyết / Sương độc / Nắng gắt)
        if (data.weatherDuration > 0)
        {
            data.weatherDuration--;
            if (data.currentWeather == WeatherEvent.Blizzard) baseTemp -= 15.0f; // Rét buốt -30°C
        }
        else
        {
            data.currentWeather = WeatherEvent.Clear;
        }

        data.environmentTemperature = baseTemp;
        PostWeatherChanged();
    }

    private void ProcessMacroMonthlyConsumption()
    {
        int foodPerCapita = data.CurrentSeason == SeasonType.Winter ? 2 : 1;
        int needFood = data.totalPopulation * foodPerCapita;
        int consumedFood = Mathf.Min(data.stockFood, needFood);
        data.stockFood -= consumedFood;

        int peopleFed = foodPerCapita > 0 ? consumedFood / foodPerCapita : 0;
        int peopleHungry = Mathf.Max(0, data.totalPopulation - peopleFed);
        LastHungryRate = data.totalPopulation > 0
            ? (float)peopleHungry / data.totalPopulation
            : 0f;

        data.riotRiskMeter = Mathf.Min(100f, data.riotRiskMeter + LastHungryRate * 10f);
        GameEvents.Post(EventID.HungerResolved, LastHungryRate);

        int coalPerCapita = data.CurrentSeason == SeasonType.Winter ? 1 : 0;
        int needCoal = data.totalPopulation * coalPerCapita;
        int consumedCoal = Mathf.Min(data.stockCoal, needCoal);
        data.stockCoal -= consumedCoal;

        int peopleWarm = coalPerCapita > 0 ? consumedCoal / coalPerCapita : data.totalPopulation;
        int peopleCold = Mathf.Max(0, data.totalPopulation - peopleWarm);
        LastColdRate = data.totalPopulation > 0
            ? (float)peopleCold / data.totalPopulation
            : 0f;
        data.riotRiskMeter = Mathf.Min(100f, data.riotRiskMeter + LastColdRate * 10f);
        GameEvents.Post(EventID.ColderResolved, LastColdRate);

        LastDiseasePressure = CalculateDiseasePressure();
        GameEvents.Post(EventID.DiseasePressure, LastDiseasePressure);
    }

    private float CalculateDiseasePressure()
    {
        float pressure = 0.015f;

        if (data.CurrentSeason == SeasonType.Winter)
            pressure += 0.02f;

        if (data.currentWeather == WeatherEvent.Blizzard)
            pressure += 0.025f;
        else if (data.currentWeather == WeatherEvent.ToxicFog)
            pressure += 0.05f;

        pressure += LastHungryRate * 0.03f;
        pressure += LastColdRate * 0.03f;

        return Mathf.Clamp01(pressure);
    }

    // ==========================================
    // CÁC HÀM TIỆN ÍCH TƯƠNG TÁC TÀI NGUYÊN (API)
    // ==========================================

    public void ModifyReputation(float amount)
    {
        data.reputation = Mathf.Clamp(
            data.reputation + amount,
            ImmigrationRules.MinReputation,
            ImmigrationRules.MaxReputation);
        GameEvents.Post(EventID.ResourcesChanged);
    }

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

    private void PostMonthChanged()
    {
        GameEvents.Post(EventID.MonthChanged, new MonthChangedPayload
        {
            year = data.currentYear,
            month = data.currentMonth,
            season = data.CurrentSeason
        });
    }

    private void PostWeatherChanged()
    {
        GameEvents.Post(EventID.WeatherChanged, new WeatherChangedPayload
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