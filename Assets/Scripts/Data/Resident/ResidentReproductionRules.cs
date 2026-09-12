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

    public static bool CanBeMother(in ResidentData resident) =>
        CanReproduce(in resident) && resident.gender == GenderType.Female;

    public static bool CanBeFather(in ResidentData resident) =>
        CanReproduce(in resident) && resident.gender == GenderType.Male;

    public static float GetBirthChance(in ResidentData mother, in ResidentData father, SeasonType season)
    {
        float chance = BaseMonthlyChance
            + ((mother.happiness + father.happiness) * 0.5f) * 0.004f
            + ((mother.wealth + father.wealth) * 0.5f / 100f) * 0.003f;

        FactionType faction = mother.factionType;
        chance *= faction switch
        {
            FactionType.Aristocrat => 0.7f,
            FactionType.Commoner => 1.1f,
            FactionType.Zealot => 0.9f,
            _ => 1f
        };

        chance *= mother.originRegion switch
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
