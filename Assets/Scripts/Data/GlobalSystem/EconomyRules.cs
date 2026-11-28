using UnityEngine;

/// <summary>Mức lương người chơi đặt (GDD v0.3 mục 6.4) — nhân vào lương theo nghề.</summary>
public enum WageLevel : byte { VeryLow = 0, Low = 1, Normal = 2, High = 3, VeryHigh = 4 }

/// <summary>Chính sách khi hộ không đủ tiền ăn / sưởi (GDD 6.4).</summary>
public enum FeedPolicy : byte
{
    Subsidize = 0,   // Cấp phát: vẫn cho ăn, thành chịu lỗ
    CutRations = 1   // Cắt khẩu phần: hộ đó bị đói dù kho còn lương thực
}

/// <summary>
/// Số liệu kinh tế GDD v0.3 mục 6.4 (lương, thuế thu nhập, tiền ăn / sưởi của hộ, nợ lương → đình công).
/// Mọi số là "đề xuất" của GDD, cần chạy mô phỏng — chỉnh ở đây.
/// </summary>
public static class EconomyRules
{
    // ===== Lương theo nghề (vàng / tháng, trước khi nhân mức lương) =====
    public const float WageCommon = 6f;          // Lao động phổ thông
    public const float WageSkilledLabor = 8f;    // Farmer / Miner
    public const float WageExpert = 12f;         // Doctor / Scholar (Teacher)

    public static float GetBaseWage(ProfessionType p) => p switch
    {
        ProfessionType.Farmer or ProfessionType.Miner => WageSkilledLabor,
        ProfessionType.Doctor or ProfessionType.Teacher => WageExpert,
        _ => WageCommon
    };

    public static float GetWageMultiplier(WageLevel level) => level switch
    {
        WageLevel.VeryLow => 0.5f,
        WageLevel.Low => 0.75f,
        WageLevel.High => 1.25f,
        WageLevel.VeryHigh => 1.5f,
        _ => 1f
    };

    public static string WageLabel(WageLevel level) => level switch
    {
        WageLevel.VeryLow => "Rất thấp",
        WageLevel.Low => "Thấp",
        WageLevel.High => "Cao",
        WageLevel.VeryHigh => "Rất cao",
        _ => "Vừa"
    };

    // ===== Thuế thu nhập (cùng 5 mức với thuế nhập thành, % lương) =====
    public static float GetIncomeTaxRate(EntryTaxLevel level) => StabilityRules.GetEntryTaxPercent(level) / 100f;

    /// <summary>Happiness cảm nhận theo thuế thu nhập (ngoài ảnh hưởng qua lương ròng): Miễn +5, Thấp +2, Vừa 0, Cao −5, Hà khắc −10.</summary>
    public static float GetIncomeTaxHappiness(EntryTaxLevel level) => level switch
    {
        EntryTaxLevel.Exempt => 5f,
        EntryTaxLevel.Low => 2f,
        EntryTaxLevel.High => -5f,
        EntryTaxLevel.Harsh => -10f,
        _ => 0f
    };

    // ===== Chi phí sinh hoạt hộ trả cho thành (Đợt 4 sẽ theo giá thị trường) =====
    public const float FoodFeePerPerson = 3f;    // Mỗi người (kể cả trẻ em, người già) / tháng
    public const float HeatFeePerPerson = 2f;    // Mùa đông, nhà được sưởi

    // ===== Lương thực tế -> Happiness (GDD 9.6) =====
    public const float RealWagePivot = 1.1f;
    public const float RealWageScale = 30f;
    public const float RealWageMin = -20f;
    public const float RealWageMax = 15f;

    /// <summary>30 × (r − 1,1), giới hạn −20…+15; r = lương ròng của hộ ÷ chi phí sinh hoạt của hộ.</summary>
    public static float GetRealWageModifier(float ratio) =>
        Mathf.Clamp(RealWageScale * (ratio - RealWagePivot), RealWageMin, RealWageMax);

    // ===== Nợ lương → đình công (GDD 6.4) =====
    public const float WageDebtHappiness = -10f;
    public const float StrikeHappiness = -20f;
    public const int StrikeAfterDebtMonths = 2;      // Nợ 2 tháng liên tiếp -> đình công
    public const int MarchAfterStrikeMonths = 2;     // Đình công 2 tháng -> kéo lên Tòa thị chính
    public const float MarchRiot = 10f;
}
