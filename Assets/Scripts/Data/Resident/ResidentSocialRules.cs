public static class ResidentSocialRules
{
    public const byte MaxWealth = 100;
    public const float PoorHappinessPenalty = 0.04f;
    public const float ModestHappinessPenalty = 0.02f;
    public const float RichHappinessGain = 0.02f;
    public const float AristocratPovertyPenalty = 0.03f;
    public const float GreenZoneHappinessGain = 0.01f;
    public const float RedZoneHappinessPenalty = 0.01f;
    public const float MaxMonthlyRiotFromSocial = 8f;

    public static void EnsureFaction(ref ResidentData resident)
    {
        if (resident.factionType == FactionType.None)
            resident.factionType = FactionType.Commoner;
    }

    public static ProfessionType ApplyFactionProfessionBias(in ResidentData resident, ProfessionType fromStat)
    {
        switch (resident.factionType)
        {
            case FactionType.Scholar:
                if (fromStat == ProfessionType.Craftsman)
                    return ProfessionType.Teacher;
                break;
            case FactionType.Aristocrat:
                if (fromStat == ProfessionType.Farmer)
                    return ProfessionType.Guard;
                if (fromStat == ProfessionType.Miner || fromStat == ProfessionType.Lumberjack)
                    return ProfessionType.None;
                break;
            case FactionType.Zealot:
                if (fromStat == ProfessionType.Farmer)
                    return ProfessionType.Guard;
                break;
        }

        return fromStat;
    }

    public static int GetProfessionAffinity(in ResidentData resident, ProfessionType profession)
    {
        int affinity = resident.factionType switch
        {
            FactionType.Aristocrat => profession switch
            {
                ProfessionType.Doctor or ProfessionType.Teacher or ProfessionType.Craftsman => 4,
                ProfessionType.Guard => 2,
                _ => 0
            },
            FactionType.Scholar => profession switch
            {
                ProfessionType.Doctor or ProfessionType.Teacher => 4,
                ProfessionType.Craftsman or ProfessionType.Guard => 2,
                _ => 0
            },
            FactionType.Zealot => profession switch
            {
                ProfessionType.Guard => 4,
                ProfessionType.Teacher => 2,
                _ => 1
            },
            _ => profession switch
            {
                ProfessionType.Farmer or ProfessionType.Miner
                    or ProfessionType.Lumberjack or ProfessionType.Craftsman => 3,
                _ => 1
            }
        };

        if (resident.originRegion == OriginRegion.RedZone
            && (profession == ProfessionType.Guard
                || profession == ProfessionType.Farmer
                || profession == ProfessionType.Miner
                || profession == ProfessionType.Lumberjack))
            affinity++;
        else if (resident.originRegion == OriginRegion.GreenZone
            && (profession == ProfessionType.Doctor
                || profession == ProfessionType.Teacher
                || profession == ProfessionType.Craftsman))
            affinity++;

        return affinity;
    }

    public static int GetHousingPriorityBonus(in ResidentData resident)
    {
        int bonus = resident.wealth / 10;
        bonus += resident.factionType switch
        {
            FactionType.Aristocrat => 8,
            FactionType.Scholar => 4,
            FactionType.Zealot => 2,
            _ => 0
        };
        return bonus;
    }

    public static float GetInfectionChanceMultiplier(OriginRegion origin) => origin switch
    {
        OriginRegion.GreenZone => 0.75f,
        OriginRegion.RedZone => 1.35f,
        _ => 1f
    };

    public static int GetMonthlyWealthDelta(in ResidentData resident)
    {
        if (!resident.isAlive)
            return 0;

        int delta = 0;
        AgeGroupType ageGroup = resident.GetAgeGroup();
        if (ageGroup == AgeGroupType.Elderly)
            delta = -1;
        else if (ResidentAssignmentRules.CanWork(in resident) && resident.assignedWorkID >= 0)
        {
            delta = resident.professionType switch
            {
                ProfessionType.Doctor or ProfessionType.Teacher => 3,
                ProfessionType.Craftsman or ProfessionType.Guard => 2,
                _ => 1
            };
            if (resident.factionType == FactionType.Aristocrat)
                delta++;
        }
        else if (ageGroup == AgeGroupType.Adult)
            delta = -1;

        if (resident.hungerMonths > 0 || resident.coldMonths > 0)
            delta--;

        return delta;
    }

    public static void ApplyWealthDelta(ref ResidentData resident, int delta)
    {
        int next = resident.wealth + delta;
        if (next < 0)
            next = 0;
        else if (next > MaxWealth)
            next = MaxWealth;
        resident.wealth = (byte)next;
    }

    public static float GetMonthlyHappinessDelta(in ResidentData resident)
    {
        float delta = 0f;
        if (resident.wealth < 20)
            delta -= PoorHappinessPenalty;
        else if (resident.wealth < 40)
            delta -= ModestHappinessPenalty;
        else if (resident.wealth >= 70)
            delta += RichHappinessGain;

        if (resident.factionType == FactionType.Aristocrat && resident.wealth < 50)
            delta -= AristocratPovertyPenalty;

        if (resident.originRegion == OriginRegion.GreenZone)
            delta += GreenZoneHappinessGain;
        else if (resident.originRegion == OriginRegion.RedZone)
            delta -= RedZoneHappinessPenalty;

        return delta;
    }

    public static float GetRiotContribution(in ResidentData resident)
    {
        if (!resident.isAlive)
            return 0f;

        float contrib = 0f;
        if (resident.wealth < 25)
            contrib += 0.02f;
        if (resident.happiness < 0.35f)
            contrib += 0.03f;
        if (resident.factionType == FactionType.Aristocrat && resident.happiness < 0.5f)
            contrib += 0.04f;
        if (resident.factionType == FactionType.Zealot && resident.happiness < 0.4f)
            contrib += 0.05f;
        if (resident.originRegion == OriginRegion.RedZone && resident.wealth < 30)
            contrib += 0.02f;
        return contrib;
    }
}
