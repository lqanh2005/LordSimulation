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
    public float miasma;           // Mức khí độc Miasma: tăng áp lực dịch bệnh
    public SeasonType CurrentSeason => (currentMonth - 1) switch
    {
        >= 0 and <= 2 => SeasonType.Spring,
        >= 3 and <= 5 => SeasonType.Summer,
        >= 6 and <= 8 => SeasonType.Autumn,
        _ => SeasonType.Winter
    };
}