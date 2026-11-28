using UnityEngine;

/// <summary>
/// Hiệu ứng thụ động hàng tháng của công trình không cần thợ (CityHall, WaterWell, Campfire, trang trí).
/// Áp dụng trong GlobalSystemManager khi sang tháng.
/// </summary>
public static class BuildingEffectRules
{
    // Tòa thị chính: thu thuế + uy tín (chỉ được xây 1)

    // Giếng nước: giảm áp lực dịch bệnh + nơi dân tụ tập, giảm nguy cơ bạo loạn (nhận lại từ Xe goòng cũ)
    public const float WellDiseaseReduction = 0.004f;
    public const float WellDiseaseReductionMax = 0.012f;
    public const float WellRiotReduction = 0.3f;

    // Lửa trại: sưởi ấm miễn phí vào mùa đông (không tốn than)
    public const int CampfireWarmPeople = 6;

    // Trang trí (Xe goòng trong save cũ): giảm nguy cơ bạo loạn. Trần chung với Giếng nước.
    public const float DecorRiotReduction = 0.3f;
    public const float DecorRiotReductionMax = 2f;

    public struct MonthlyEffects
    {
        public int gold;
        public float reputation;
        public bool hasTaxOffice;
        public float diseaseReduction;
        public int warmPeople;
        public float riotReduction;
        public int wells;   // Số Giếng nước đang hoạt động -> sức cấp nước (WaterRules)
    }

    public static bool IsUnique(BuildingType type) => type == BuildingType.CityHall;

    public static bool IsDecoration(BuildingType type) => type == BuildingType.CoalCart;

    public static bool IsEffectActive(in BuildingData building) =>
        building.isAllocated
        && building.buildingState == BuildingState.Active
        && building.isOperational;

    public static MonthlyEffects Collect(BuildingData[] buildings, int count)
    {
        MonthlyEffects fx = default;
        if (buildings == null)
            return fx;

        bool hasCityHall = false;
        for (int i = 0; i < count; i++)
        {
            ref BuildingData b = ref buildings[i];
            if (!IsEffectActive(in b))
                continue;

            switch (b.buildingType)
            {
                case BuildingType.CityHall:
                    hasCityHall = true;
                    break;
                case BuildingType.WaterWell:
                    fx.diseaseReduction += WellDiseaseReduction;
                    fx.riotReduction += WellRiotReduction;
                    fx.wells++;
                    break;
                case BuildingType.Campfire:
                    fx.warmPeople += CampfireWarmPeople;
                    break;
                default:
                    if (IsDecoration(b.buildingType))
                        fx.riotReduction += DecorRiotReduction;
                    break;
            }
        }

        // GDD v0.3 mục 6.4: Tòa thị chính đóng vai Tax Office — mở thuế thu nhập (EconomySystem),
        // không còn tự sinh vàng / uy tín mỗi tháng
        fx.hasTaxOffice = hasCityHall;

        fx.diseaseReduction = Mathf.Min(fx.diseaseReduction, WellDiseaseReductionMax);
        fx.riotReduction = Mathf.Min(fx.riotReduction, DecorRiotReductionMax);
        return fx;
    }
}
