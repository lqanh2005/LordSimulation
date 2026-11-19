using UnityEngine;

/// <summary>
/// Nước uống: Giếng nước là nguồn nước của thành. Suối tự nhiên đủ cho một nhóm nhỏ,
/// dân đông hơn sức cấp nước thì một phần bị khát mỗi tháng:
/// giảm hạnh phúc, tăng bạo loạn, phải uống nước bẩn (tăng áp lực dịch bệnh),
/// khát liên tiếp nhiều tháng thì có thể chết khát. Mô phỏng theo tháng như lương thực.
/// </summary>
public static class WaterRules
{
    public const int BaseWaterCapacity = 10;          // Suối / nước mưa: đủ cho nhóm đầu tiên
    public const int WellWaterCapacity = 15;          // Mỗi Giếng nước đang hoạt động

    public const float ThirstHappinessPenalty = 0.12f;
    public const float ThirstRiotFactor = 10f;        // Bạo loạn += tỉ lệ khát x hệ số (như đói)
    public const float ThirstDiseaseFactor = 0.04f;   // Uống nước bẩn -> áp lực dịch bệnh
    public const byte ThirstMonthsBeforeDeathRisk = 2;
    public const float ThirstDeathChance = 0.12f;
    public const float ThirstDiseaseDeathBonus = 0.02f;   // Đang bệnh mà khát thì dễ chết hơn
    public const float ThirstAgingDeathBonus = 0.05f;     // Người già khát

    public static int GetCapacity(int activeWells) =>
        BaseWaterCapacity + Mathf.Max(0, activeWells) * WellWaterCapacity;

    /// <summary>Tỉ lệ dân bị khát (0..1) khi dân số vượt sức cấp nước.</summary>
    public static float GetThirstyRate(int population, int capacity)
    {
        if (population <= 0)
            return 0f;
        int thirsty = Mathf.Max(0, population - Mathf.Max(0, capacity));
        return (float)thirsty / population;
    }

    /// <summary>Số giếng cần thêm để đủ nước cho dân số hiện tại.</summary>
    public static int WellsNeeded(int population, int capacity)
    {
        int missing = population - capacity;
        return missing <= 0 ? 0 : Mathf.CeilToInt((float)missing / WellWaterCapacity);
    }
}
