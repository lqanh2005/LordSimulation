using UnityEngine;

public static class ResidentReproductionRules
{
    public const byte MinFertileAge = 18;
    public const byte MaxFertileAge = 40;
    public const float MinHappiness = 0.35f;
    public const float BaseMonthlyChance = 0.005f;
    public const float MaxMonthlyChance = 0.02f;

    public static bool CanReproduce(in ResidentData resident)
    {
        if (!resident.isAlive)
            return false;

        if (resident.age < MinFertileAge || resident.age > MaxFertileAge)
            return false;

        if (resident.GetAgeGroup() != AgeGroupType.Adult)
            return false;

        if (resident.healthStatus == HealthStatus.ActiveInfected)
            return false;

        if (resident.assignedHouseID < 0)
            return false;

        if (resident.hungerMonths > 0 || resident.coldMonths > 0)
            return false;

        return resident.happiness >= MinHappiness;
    }

    public static float GetBirthChance(in ResidentData resident, SeasonType season)
    {
        float chance = BaseMonthlyChance
            + resident.happiness * 0.004f
            + (resident.wealth / 100f) * 0.003f;

        chance *= resident.factionType switch
        {
            FactionType.Aristocrat => 0.7f,
            FactionType.Commoner => 1.1f,
            FactionType.Zealot => 0.9f,
            _ => 1f
        };

        chance *= resident.originRegion switch
        {
            OriginRegion.GreenZone => 1.1f,
            OriginRegion.RedZone => 0.85f,
            _ => 1f
        };

        chance *= season switch
        {
            SeasonType.Spring => 1.2f,
            SeasonType.Winter => 0.75f,
            _ => 1f
        };

        if (chance < 0f)
            return 0f;
        return chance > MaxMonthlyChance ? MaxMonthlyChance : chance;
    }

    public static ResidentData CreateChild(in ResidentData parent, int residentId)
    {
        byte wealth = (byte)(parent.wealth / 4);
        return new ResidentData
        {
            residentID = residentId,
            firstNameID = (ushort)Random.Range(0, NameDatabase.FirstNames.Length),
            lastNameID = parent.lastNameID,
            age = 0,
            originRegion = parent.originRegion,
            factionType = parent.factionType == FactionType.None
                ? FactionType.Commoner
                : parent.factionType,
            wealth = wealth,
            professionType = ProfessionType.Student,
            healthStatus = HealthStatus.Healthy,
            diseaseType = DiseaseType.None,
            IncubationMonths = 0,
            recoveryMonths = 0,
            happiness = 0.6f,
            bodyTemperature = ResidentDiseaseRules.NormalBodyTemperature,
            symptoms = SymptomFlags.None,
            assignedHouseID = parent.assignedHouseID,
            assignedWorkID = ResidentAssignmentRules.UnassignedId,
            isAlive = true,
            hungerMonths = 0,
            coldMonths = 0,
            strength = InheritStat(parent.strength),
            endurance = InheritStat(parent.endurance),
            intellect = InheritStat(parent.intellect)
        };
    }

    private static byte InheritStat(byte parentStat)
    {
        int next = parentStat + Random.Range(-8, 9);
        if (next < 10)
            next = 10;
        else if (next > 100)
            next = 100;
        return (byte)next;
    }
}
