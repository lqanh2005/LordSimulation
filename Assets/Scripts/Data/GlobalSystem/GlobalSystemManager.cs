using System;
using UnityEngine;

public class GlobalSystemManager : MonoBehaviour
{
    [Header("Cấu Hình Thời Gian Mô Phỏng")]
    [Tooltip("Thời gian thực (giây) cho 1 tháng in-game")]
    [SerializeField] private float realSecondsPerMonth = 150f; // 2.5 phút thực = 1 tháng

    // Dữ liệu lõi toàn cầu (65 bytes)
    [SerializeField] private GlobalSystemData data;

    // --- CÁC SỰ KIỆN TOÀN CỤC (EVENTS CHO UI/SYSTEMS ĐĂNG KÝ) ---
    public event Action<int, byte, SeasonType> OnMonthChanged; // year, month, season
    public event Action<int> OnYearChanged;                    // year
    public event Action<WeatherEvent, float> OnWeatherChanged; // weather, temp
    public event Action OnResourcesChanged;                    // Khi vàng/kho thay đổi

    public void Init()
    {
        // Khởi tạo mặc định nếu là ván chơi mới (New Game)
        if (data.currentYear == 0)
        {
            InitializeNewGame();
        }
    }

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

    private void InitializeNewGame()
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
        OnMonthChanged?.Invoke(data.currentYear, data.currentMonth, data.CurrentSeason);
        OnWeatherChanged?.Invoke(data.currentWeather, data.environmentTemperature);
        OnResourcesChanged?.Invoke();
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
            OnYearChanged?.Invoke(data.currentYear);
        }

        // 1. Tính toán lại nhiệt độ môi trường theo Mùa
        UpdateSeasonalEnvironment();

        // 2. Trừ tiêu hao tài nguyên & kiểm tra nguy cơ bạo loạn
        ProcessMacroMonthlyConsumption();

        // 3. Phát event thông báo sang tháng mới cho toàn bộ hệ thống
        OnMonthChanged?.Invoke(data.currentYear, data.currentMonth, data.CurrentSeason);
        OnResourcesChanged?.Invoke();
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
        OnWeatherChanged?.Invoke(data.currentWeather, data.environmentTemperature);
    }

    private void ProcessMacroMonthlyConsumption()
    {
        // Trừ điểm quyền lực duy trì các sắc lệnh đang bật
        // (Phần này sẽ tương tác với EdictManager)
    }

    // ==========================================
    // CÁC HÀM TIỆN ÍCH TƯƠNG TÁC TÀI NGUYÊN (API)
    // ==========================================

    public void ModifyGold(int amount)
    {
        data.treasuryGold = Mathf.Max(0, data.treasuryGold + amount);
        OnResourcesChanged?.Invoke();
    }

    public bool TryConsumeFood(int amount)
    {
        if (data.stockFood >= amount)
        {
            data.stockFood -= amount;
            OnResourcesChanged?.Invoke();
            return true;
        }
        return false;
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
        OnResourcesChanged?.Invoke();
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