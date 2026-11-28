using UnityEngine;

public static class ResidentSocialSystem
{
    public static int LastMonthTax { get; private set; }

    /// <summary>Riot cộng thêm trong lần tính tháng gần nhất (theo công thức GDD 9.6, chưa gồm sự kiện / sắc lệnh).</summary>
    public static float LastRiotDelta { get; private set; }

    /// <summary>Happiness trung bình của Adult (0–100) dùng cho công thức Riot tháng gần nhất.</summary>
    public static float LastAdultHappiness { get; private set; } = StabilityRules.HappinessBase;

    // Loại công trình theo id — dựng lại mỗi tháng để tra loại nhà của cư dân
    private static readonly BuildingType[] TypeById = new BuildingType[ushort.MaxValue + 1];
    private static readonly bool[] CrowdedById = new bool[ushort.MaxValue + 1];   // Nhà đang chứa quá sức (Dồn người)
    private static int _gateClerkId;
    private static float _incomeTaxHappiness;   // Gate Clerk có việc ở Cổng (không tính thất nghiệp)

    public static void ResetStats()
    {
        LastMonthTax = 0;
        LastRiotDelta = 0f;
        LastAdultHappiness = StabilityRules.HappinessBase;
    }

    public static void ProcessMonthly(
        ResidentData[] residents,
        int residentCount,
        BuildingData[] buildings,
        int buildingCount,
        GlobalSystemManager global)
    {
        LastMonthTax = 0;
        if (residents == null)
            return;

        FillBuildingTypes(buildings, buildingCount);
        _gateClerkId = global != null ? global.GetGlobalDataRef().gateClerkResidentID : 0;
        _incomeTaxHappiness = global != null ? EconomyRules.GetIncomeTaxHappiness(global.GetGlobalDataRef().incomeTaxLevel) : 0f;
        bool isWinter = global != null && global.GetGlobalDataRef().CurrentSeason == SeasonType.Winter;

        int alive = 0;
        int adults = 0;
        float adultHappinessSum = 0f;
        for (int i = 0; i < residentCount; i++)
        {
            ref ResidentData r = ref residents[i];
            if (!r.isAlive)
                continue;

            alive++;
            ResidentSocialRules.EnsureFaction(ref r);
            // Lương / thuế / Wealth tính ở EconomySystem (GDD v0.3 mục 6.4), chạy ngay trước hệ thống này

            // GDD 9.6: Happiness tiến dần về mức mục tiêu
            float target = GetHappinessTarget(in r, isWinter);
            r.happiness = StabilityRules.ApproachHappiness(r.happiness, target);
            if (r.griefMonths > 0)
                r.griefMonths--;

            if (r.GetAgeGroup() == AgeGroupType.Adult)
            {
                adults++;
                adultHappinessSum += r.happiness * 100f;
            }
        }

        if (global == null)
            return;

        ref GlobalSystemData data = ref global.GetGlobalDataRef();

        LastMonthTax = EconomySystem.LastReport.incomeTax;

        // GDD 9.6: Riot tăng = (45 − H Adult)/2,5 + đình công (chưa có) + 5 nếu Infirmary quá tải. Không xuống dưới 0.
        if (alive <= 0)
        {
            LastRiotDelta = 0f;
            return;
        }

        LastAdultHappiness = adults > 0 ? adultHappinessSum / adults : StabilityRules.HappinessBase;
        LastRiotDelta = StabilityRules.GetMonthlyRiotDelta(
            LastAdultHappiness, EconomySystem.LastReport.strikeRate, ResidentDiseaseSystem.LastInfirmaryOverloaded);
        data.riotRiskMeter = Mathf.Clamp(data.riotRiskMeter + LastRiotDelta, 0f, 100f);
    }

    /// <summary>Mục tiêu Happiness (0–100) = 50 + các điều chỉnh của GDD 9.6.</summary>
    public static float GetHappinessTarget(in ResidentData r, bool isWinter)
    {
        float target = StabilityRules.HappinessBase;

        // Lương thực tế của hộ: 30 × (r − 1,1), giới hạn −20…+15 (EconomySystem tính r)
        if (EconomySystem.TryGetRealWageModifier(r.residentID, out float realWage))
            target += realWage;

        // Thuế thu nhập cảm nhận (ngoài ảnh hưởng qua lương ròng) — chỉ người có lương mới cảm thấy
        if (r.assignedWorkID >= 0 || r.IsStriking)
            target += _incomeTaxHappiness;

        // Nợ lương / đình công (GDD 6.4)
        if (r.IsStriking)
            target += EconomyRules.StrikeHappiness;
        else if (r.wageDebt > 0)
            target += EconomyRules.WageDebtHappiness;

        if (r.hungerMonths > 0 || r.thirstMonths > 0)
            target += StabilityRules.HungryModifier;
        if (isWinter && r.coldMonths > 0)
            target += StabilityRules.ColdModifier;

        if (r.assignedHouseID < 0)
            target += StabilityRules.HomelessModifier;
        else
        {
            BuildingType house = TypeById[(ushort)r.assignedHouseID];
            if (house == BuildingType.Tent)
                target += StabilityRules.TentModifier;
            else if (house == BuildingType.House || house == BuildingType.LargeHouse)
                target += StabilityRules.GoodHouseModifier;
        }

        if (r.GetAgeGroup() == AgeGroupType.Adult
            && ResidentAssignmentRules.CanWork(in r)
            && r.assignedWorkID < 0
            && (_gateClerkId == 0 || r.residentID != _gateClerkId))
            target += StabilityRules.UnemployedAdultModifier;

        if (r.healthStatus == HealthStatus.ActiveInfected)
            target += StabilityRules.SickModifier;

        if (r.griefMonths > 0)
            target += StabilityRules.GriefModifier;

        // Lệnh khẩn cấp (GDD 9.4): thiết quân luật, đốt xác, dồn người, thợ mỏ bất mãn
        target += EmergencyOrders.GetHappinessModifier(in r, r.assignedHouseID >= 0 && CrowdedById[(ushort)r.assignedHouseID]);

        return target;
    }

    private static void FillBuildingTypes(BuildingData[] buildings, int buildingCount)
    {
        System.Array.Clear(TypeById, 0, TypeById.Length);
        System.Array.Clear(CrowdedById, 0, CrowdedById.Length);
        if (buildings == null)
            return;
        for (int i = 0; i < buildingCount; i++)
        {
            TypeById[buildings[i].buildingID] = buildings[i].buildingType;
            CrowdedById[buildings[i].buildingID] = buildings[i].currentOccupancy > buildings[i].maxOccupancy;
        }
    }
}
