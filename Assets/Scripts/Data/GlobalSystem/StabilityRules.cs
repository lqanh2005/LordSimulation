using UnityEngine;

/// <summary>Mức thuế nhập thành (GDD v0.3 mục 6.4). Lưu theo byte trong save v12.</summary>
public enum EntryTaxLevel : byte { Exempt = 0, Low = 1, Normal = 2, High = 3, Harsh = 4 }

/// <summary>
/// Công thức Happiness / hiệu suất / Riot / Reputation / thuế nhập thành theo GDD v0.3 mục 9.6 + 6.4.
/// Mọi số là "đề xuất" của GDD, giữ ở đây để chỉnh một chỗ. Happiness của cư dân lưu 0–1 trong ResidentData,
/// công thức tính theo thang 0–100 như GDD rồi quy đổi.
/// </summary>
public static class StabilityRules
{
    // ===== Happiness (0–100) =====
    public const float HappinessBase = 50f;
    public const float HappinessApproachRate = 0.3f;     // H mới = H + 0,3 × (mục tiêu − H)
    public const float HungryModifier = -20f;            // Đói (thiếu Food / cắt khẩu phần) — khát tính như đói
    public const float ColdModifier = -15f;              // Mùa đông, nhà không được sưởi
    public const float GoodHouseModifier = 5f;           // Nhà gỗ trở lên
    public const float TentModifier = -5f;
    public const float HomelessModifier = -20f;
    public const float UnemployedAdultModifier = -10f;
    public const float SickModifier = -10f;
    public const float GriefModifier = -15f;             // Người thân (cùng nhà) chết trong 3 tháng qua
    public const byte GriefMonths = 3;

    /// <summary>Hiệu suất lao động = 0,6 + 0,8 × H/100 (H 50 → 1,0; 0 → 0,6; 100 → 1,4).</summary>
    public static float GetWorkEfficiency(float happiness01) => 0.6f + 0.8f * Mathf.Clamp01(happiness01);

    public static float ApproachHappiness(float current01, float target100)
    {
        float current = current01 * 100f;
        float target = Mathf.Clamp(target100, 0f, 100f);
        float next = current + HappinessApproachRate * (target - current);
        return Mathf.Clamp01(next / 100f);
    }

    // ===== Riot Meter (0–100) =====
    public const float RiotHappinessPivot = 45f;
    public const float RiotHappinessDivisor = 2.5f;
    public const float RiotPerStrikeTenPercent = 3f;     // Chưa có đình công (Đợt 3) — để sẵn
    public const float RiotInfirmaryOverload = 5f;
    public const float RiotCriticalThreshold = 70f;

    /// <summary>Riot tăng mỗi tháng = (45 − H trung bình Adult)/2,5 + 3 × (%đình công/10) + 5 nếu Infirmary quá tải.</summary>
    public static float GetMonthlyRiotDelta(float avgAdultHappiness100, float strikeRate01, bool infirmaryOverloaded)
    {
        float delta = (RiotHappinessPivot - avgAdultHappiness100) / RiotHappinessDivisor;
        delta += RiotPerStrikeTenPercent * (strikeRate01 * 100f / 10f);
        if (infirmaryOverloaded)
            delta += RiotInfirmaryOverload;
        return delta;
    }

    // ===== Reputation (0–100, bắt đầu 50) =====
    public const float ReputationStart = 50f;
    public const float ReputationMin = 0f;
    public const float ReputationMax = 100f;
    public const float ReputationPullRate = 0.1f;        // Mỗi tháng kéo về 50 một đoạn 10% chênh lệch
    public const float AdmitReputation = 1f;
    public const float AdmitNeedyReputation = 2f;        // Có note sức khoẻ hoặc Nghèo kiệt
    public const float DenyReputation = -1f;
    public const float DenyChildReputation = -2f;
    public const float YardDeathReputation = -2f;
    public const float YardLeaveReputation = -1f;
    public const byte PoorWealthThreshold = 10;          // "Nghèo kiệt": không trả nổi thuế nhập thành

    public static float PullTowardStart(float reputation) =>
        reputation + (ReputationStart - reputation) * ReputationPullRate;

    // ===== Thuế nhập thành =====
    public const EntryTaxLevel DefaultEntryTax = EntryTaxLevel.Normal;

    public static float GetEntryTaxPercent(EntryTaxLevel level) => level switch
    {
        EntryTaxLevel.Exempt => 0f,
        EntryTaxLevel.Low => 10f,
        EntryTaxLevel.High => 35f,
        EntryTaxLevel.Harsh => 50f,
        _ => 20f
    };

    /// <summary>Reputation mỗi tháng theo mức thuế nhập thành: Miễn +2, Thấp +1, Vừa 0, Cao −1, Hà khắc −3.</summary>
    public static float GetEntryTaxReputation(EntryTaxLevel level) => level switch
    {
        EntryTaxLevel.Exempt => 2f,
        EntryTaxLevel.Low => 1f,
        EntryTaxLevel.High => -1f,
        EntryTaxLevel.Harsh => -3f,
        _ => 0f
    };

    /// <summary>Tỷ lệ người giàu tìm đến theo thuế nhập thành: Miễn 30%, Thấp 25%, Vừa 20%, Cao 10%, Hà khắc 5%.</summary>
    public static float GetRichArrivalRate(EntryTaxLevel level) => level switch
    {
        EntryTaxLevel.Exempt => 0.30f,
        EntryTaxLevel.Low => 0.25f,
        EntryTaxLevel.High => 0.10f,
        EntryTaxLevel.Harsh => 0.05f,
        _ => 0.20f
    };

    public static string EntryTaxLabel(EntryTaxLevel level) => level switch
    {
        EntryTaxLevel.Exempt => "Miễn",
        EntryTaxLevel.Low => "Thấp",
        EntryTaxLevel.High => "Cao",
        EntryTaxLevel.Harsh => "Hà khắc",
        _ => "Vừa"
    };

    // ===== Đợt di cư (GDD 4.1 + 9.6) =====
    public const float YearGrowth = 1.3f;
    public const float DisasterWaveMultiplier = 1.5f;

    /// <summary>Số người gốc của đợt thường theo tháng (0 nếu tháng đó không có đợt). Mùa đông không có đợt thường.</summary>
    public static Vector2Int GetBaseWave(byte month) => month switch
    {
        2 => new Vector2Int(10, 14),   // Xuân — đợt lớn nhất
        5 => new Vector2Int(6, 8),     // Hạ
        8 => new Vector2Int(6, 8),     // Thu — đợt cuối trước mùa đông
        _ => Vector2Int.zero
    };

    public static readonly Vector2Int RefugeeWave = new Vector2Int(8, 15);   // Tị nạn thiên tai

    /// <summary>Hệ số Reputation lên quy mô đợt: 0 → ×0,6; 50 → ×1,0; 100 → ×1,4.</summary>
    public static float GetReputationWaveMultiplier(float reputation) =>
        0.6f + 0.8f * Mathf.Clamp(reputation, ReputationMin, ReputationMax) / 100f;

    /// <summary>Số người = Số gốc × 1,3^(năm − 1) × (0,6 + 0,8 × Rep/100) × (1,5 nếu có thiên tai).</summary>
    public static int RollWaveSize(Vector2Int baseRange, int year, float reputation, bool disaster)
    {
        if (baseRange.y <= 0)
            return 0;
        float count = Random.Range(baseRange.x, baseRange.y + 1);
        count *= Mathf.Pow(YearGrowth, Mathf.Max(0, year - 1));
        count *= GetReputationWaveMultiplier(reputation);
        if (disaster)
            count *= DisasterWaveMultiplier;
        int whole = Mathf.FloorToInt(count);
        if (Random.value < count - whole)
            whole++;
        return whole;
    }
}
