using UnityEngine;

/// <summary>
/// Hiệu ứng thụ động hàng tháng của công trình không cần thợ (CityHall, WaterWell, Campfire, trang trí).
/// Áp dụng trong GlobalSystemManager khi sang tháng.
/// </summary>
public static class BuildingEffectRules
{
    // Tòa thị chính: thu thuế + uy tín (chỉ được xây 1)
    public const int CityHallGoldPerMonth = 30;
    public const float CityHallReputationPerMonth = 0.5f;

    // Giếng nước: giảm áp lực dịch bệnh
    public const float WellDiseaseReduction = 0.004f;
    public const float WellDiseaseReductionMax = 0.012f;

    // Lửa trại: sưởi ấm miễn phí vào mùa đông (không tốn than)
    public const int CampfireWarmPeople = 6;

    // Trang trí: giảm nguy cơ bạo loạn
    public const float DecorRiotReduction = 0.3f;
    public const float DecorRiotReductionMax = 2f;

    public struct MonthlyEffects
    {
        public int gold;
        public float reputation;
        public float diseaseReduction;
        public int warmPeople;
        public float riotReduction;
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

        if (hasCityHall)
        {
            fx.gold += CityHallGoldPerMonth;
            fx.reputation += CityHallReputationPerMonth;
        }

        fx.diseaseReduction = Mathf.Min(fx.diseaseReduction, WellDiseaseReductionMax);
        fx.riotReduction = Mathf.Min(fx.riotReduction, DecorRiotReductionMax);
        return fx;
    }
}
