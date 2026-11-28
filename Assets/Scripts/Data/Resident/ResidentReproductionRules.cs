using UnityEngine;

public static class ResidentReproductionRules
{
    // GDD v0.3 mục 5.4 + 9.6: sinh con theo nhà
    public const int MinAdultsPerHouse = 2;
    public const float BaseHouseBirthChance = 0.05f;   // 5% / tháng ở Happiness trung bình 50

    public static bool IsFamilyHouse(BuildingType type) =>
        type == BuildingType.House || type == BuildingType.LargeHouse;   // Lều không sinh con, Trạm cách ly không tính

    /// <summary>Adult trong nhà tính là cha/mẹ được (không đang mắc bệnh / cách ly).</summary>
    public static bool CanReproduce(in ResidentData resident) =>
        resident.isAlive
        && resident.GetAgeGroup() == AgeGroupType.Adult
        && resident.healthStatus != HealthStatus.ActiveInfected
        && !ResidentAssignmentRules.NeedsQuarantineHousing(in resident);

    /// <summary>Có người trong nhà đói / khát hoặc bị lạnh (nhà không được sưởi) -> tỷ lệ sinh bằng 0.</summary>
    public static bool BlocksBirth(in ResidentData resident) =>
        resident.hungerMonths > 0 || resident.thirstMonths > 0 || resident.coldMonths > 0;

    /// <summary>Tỷ lệ sinh = 5% × Happiness trung bình của hộ / 50.</summary>
    public static float GetHouseBirthChance(float avgHappiness01) =>
        BaseHouseBirthChance * Mathf.Clamp01(avgHappiness01) * 100f / 50f;

    public static ResidentData CreateChild(in ResidentData mother, in ResidentData father, int residentId)
    {
        GenderType gender = Random.value < 0.5f ? GenderType.Male : GenderType.Female;
        byte wealth = (byte)((mother.wealth + father.wealth) / 8);
        FactionType faction = father.factionType == FactionType.None
            ? mother.factionType
            : father.factionType;
        if (faction == FactionType.None)
            faction = FactionType.Commoner;

        return new ResidentData
        {
            residentID = residentId,
            firstNameID = NameDatabase.RollFirstNameId(gender),
            lastNameID = father.lastNameID,
            age = 0,
            gender = gender,
            originRegion = mother.originRegion,
            factionType = faction,
            wealth = wealth,
            professionType = ProfessionType.Student,
            healthStatus = HealthStatus.Healthy,
            diseaseType = DiseaseType.None,
            IncubationMonths = 0,
            recoveryMonths = 0,
            happiness = 0.6f,
            bodyTemperature = ResidentDiseaseRules.NormalBodyTemperature,
            symptoms = SymptomFlags.None,
            assignedHouseID = mother.assignedHouseID,
            assignedWorkID = ResidentAssignmentRules.UnassignedId,
            familyID = mother.familyID,   // Con sinh ra thuộc hộ của mẹ
            isAlive = true,
            hungerMonths = 0,
            coldMonths = 0,
            strength = InheritStat(mother.strength, father.strength),
            endurance = InheritStat(mother.endurance, father.endurance),
            intellect = InheritStat(mother.intellect, father.intellect)
        };
    }

    private static byte InheritStat(byte motherStat, byte fatherStat)
    {
        int mid = (motherStat + fatherStat) / 2;
        int next = mid + Random.Range(-8, 9);
        if (next < 10)
            next = 10;
        else if (next > 100)
            next = 100;
        return (byte)next;
    }
}
