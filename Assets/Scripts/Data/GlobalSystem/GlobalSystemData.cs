using System;

public enum SeasonType : byte { Spring = 0, Summer = 1, Autumn = 2, Winter = 3 }
public enum WeatherEvent : byte { Clear = 0, Blizzard = 1, Heatwave = 2, ToxicFog = 3 }
public enum GameSpeed : byte { Pause = 0, Speed1x = 1, Speed2x = 2, Speed3x = 3 }
[Serializable]
public struct GlobalSystemData
{
    public ushort currentYear;
    public byte currentMonth;
    public float timeTickProgress;
    public GameSpeed currentGameSpeed;
    public float environmentTemperature;
    public WeatherEvent currentWeather;
    public byte weatherDuration;
    public int treasuryGold;
    public int stockFood;
    public int stockCoal;
    public int stockWood;
    public int stockIron;
    public int stockMedicine;
    public ushort authorityPoints;
    public float reputation;
    public float riotRiskMeter;
    public int totalPopulation;
    public int totalInfected;
    public float overallHappiness;   // % (0–100), trung bình hạnh phúc mẫu
    public bool victoryAchieved;   // Đã đạt mục tiêu MVP (sống qua mùa đông đầu) — lưu từ save v7

    // Chuỗi khủng hoảng (GDD 3.7) — lưu từ save v9
    public int corpsesFresh;       // Thi thể chết trong tháng này, chưa thu gom
    public int corpsesRotting;     // Thi thể đã để quá 1 tháng -> sinh khí độc
    public float miasma;           // Số vùng Miasma đang có hiệu lực (GDD 7.4: mỗi xác phân hủy tạo 1 vùng, kéo dài 2 tháng)

    // GDD v0.3 — lưu từ save v12
    public EntryTaxLevel entryTaxLevel;          // Thuế nhập thành đang áp dụng
    public EntryTaxLevel pendingEntryTaxLevel;   // Mức vừa chọn — có hiệu lực từ tháng sau
    public int miasmaZonesPrev;                  // Vùng Miasma sinh ra tháng trước (còn hiệu lực thêm 1 tháng)
    public bool disasterWaveBoost;               // Bão tuyết trong thành -> đợt di cư kế tiếp đông hơn 50%

    // GDD v0.3 Đợt 2 — lưu từ save v13
    public int gateClerkResidentID;              // Cư dân đang làm Gate Clerk (0 = chưa có ai)

    // GDD v0.3 Đợt 3 (mục 6.4) — lưu từ save v14. Đổi mức có hiệu lực từ tháng sau (pending*)
    public WageLevel wageLevel;
    public WageLevel pendingWageLevel;
    public EntryTaxLevel incomeTaxLevel;
    public EntryTaxLevel pendingIncomeTaxLevel;
    public FeedPolicy feedPolicy;

    // GDD v0.3 Đợt 4 (mục 6.6) — giá thị trường, lưu từ save v15. Hệ số sự kiện 0 = chưa có (coi như 1)
    public float foodPriceEvent, coalPriceEvent, woodPriceEvent;
    public MarketEventType marketActiveEvent;
    public byte marketActiveMonths;
    public float marketActiveMagnitude;
    public MarketEventType marketPendingEvent;   // Đã lên lịch, chưa xảy ra
    public byte marketPendingInMonths;
    public float marketPendingMagnitude;
    public MarketEventType rumorEvent;           // Tin đồn qua Cổng (có thể sai)
    public byte rumorInMonths;
    public bool rumorTrue;
    public bool blizzardForecast;                // Bão tuyết đã báo trước cho tháng sau (GDD 9.2)
    public SeasonType CurrentSeason => (currentMonth - 1) switch
    {
        >= 0 and <= 2 => SeasonType.Spring,
        >= 3 and <= 5 => SeasonType.Summer,
        >= 6 and <= 8 => SeasonType.Autumn,
        _ => SeasonType.Winter
    };
}