using System.Text;
using UnityEngine;

public static class ImmigrationRules
{
    public const int MaxWaiting = 32;
    public const int MinMonthlyArrivals = 1;
    public const int MaxMonthlyArrivals = 4;

    private static readonly StringBuilder Bio = new StringBuilder(512);

    public const float DefaultTariffPercent = 20f;
    public const float MinReputation = -50f;
    public const float MaxReputation = 100f;
    public const int QuarantineFoodPerMonth = 1;
    public const int QuarantineMedicinePerMonth = 1;

    public static int RollArrivalCount(SeasonType season, float reputation)
    {
        int extra = season switch
        {
            SeasonType.Spring => 2,
            SeasonType.Winter => 0,
            _ => 1
        };
        int count = MinMonthlyArrivals + extra + Random.Range(0, 2);
        if (count > MaxMonthlyArrivals)
            count = MaxMonthlyArrivals;

        float mult = 0.5f + Mathf.Clamp(reputation, MinReputation, MaxReputation) / 100f;
        if (mult < 0f)
            mult = 0f;

        int scaled = Mathf.RoundToInt(count * mult);
        return scaled > 0 ? scaled : 0;
    }

    public static int GetEntryTax(in ResidentData applicant, float tariffPercent)
    {
        float percent = tariffPercent > 0f ? tariffPercent : DefaultTariffPercent;
        int tax = Mathf.RoundToInt(applicant.wealth * percent / 100f);
        if (tax < 1 && applicant.wealth > 0)
            tax = 1;
        if (tax > applicant.wealth)
            tax = applicant.wealth;
        return tax;
    }

    public static void PayTax(ref ResidentData applicant, int tax)
    {
        int next = applicant.wealth - tax;
        applicant.wealth = next < 0 ? (byte)0 : (byte)next;
    }

    public static float GetDenyReputationGain(in ResidentData applicant)
    {
        float gain = 1.5f + applicant.wealth / 40f;
        if (applicant.factionType == FactionType.Aristocrat)
            gain += 1.5f;
        if (applicant.originRegion == OriginRegion.GreenZone)
            gain += 0.5f;
        return gain;
    }

    public static ResidentData CreateApplicant(int residentId)
    {
        GenderType gender = Random.value < 0.5f ? GenderType.Male : GenderType.Female;
        byte age = (byte)Random.Range(16, 46);
        OriginRegion origin = RollOrigin();
        FactionType faction = RollFaction();
        byte strength = (byte)Random.Range(20, 81);
        byte endurance = (byte)Random.Range(20, 81);
        byte intellect = (byte)Random.Range(20, 81);
        byte wealth = (byte)Random.Range(5, 51);
        if (faction == FactionType.Aristocrat)
            wealth = (byte)Mathf.Clamp(wealth + 30, 0, 100);

        ResidentData applicant = new ResidentData
        {
            residentID = residentId,
            firstNameID = NameDatabase.RollFirstNameId(gender),
            lastNameID = (ushort)Random.Range(0, NameDatabase.LastNames.Length),
            age = age,
            gender = gender,
            originRegion = origin,
            factionType = faction,
            wealth = wealth,
            professionType = ProfessionType.None,
            healthStatus = HealthStatus.Healthy,
            diseaseType = DiseaseType.None,
            IncubationMonths = 0,
            recoveryMonths = 0,
            happiness = Random.Range(0.35f, 0.8f),
            bodyTemperature = ResidentDiseaseRules.NormalBodyTemperature,
            symptoms = SymptomFlags.None,
            assignedHouseID = ResidentAssignmentRules.UnassignedId,
            assignedWorkID = ResidentAssignmentRules.UnassignedId,
            isAlive = true,
            hungerMonths = 0,
            coldMonths = 0,
            strength = strength,
            endurance = endurance,
            intellect = intellect
        };

        ResidentSocialRules.EnsureFaction(ref applicant);
        applicant.professionType = ResidentProfessionRules.GetInitialProfession(in applicant);
        TryApplyLatentDisease(ref applicant);
        return applicant;
    }

    public static string FormatProfile(in ResidentData applicant, RuleAction suggested)
    {
        Bio.Clear();
        Bio.Append(NameDatabase.GetFullName(applicant.firstNameID, applicant.lastNameID));
        Bio.Append(applicant.gender == GenderType.Female ? "  (Female)\n" : "  (Male)\n");
        Bio.Append("Age: ").Append(applicant.age);
        Bio.Append("   Faction: ").Append(applicant.factionType).Append('\n');
        Bio.Append("Origin: ").Append(OriginLabel(applicant.originRegion));
        Bio.Append("   Profession: ").Append(applicant.professionType).Append('\n');
        Bio.Append("Wealth: ").Append(applicant.wealth);
        Bio.Append("   Happiness: ").Append((applicant.happiness * 100f).ToString("0")).Append("%\n");
        Bio.Append("STR ").Append(applicant.strength);
        Bio.Append("  END ").Append(applicant.endurance);
        Bio.Append("  INT ").Append(applicant.intellect).Append('\n');
        Bio.Append("Temperature: ").Append(applicant.bodyTemperature.ToString("0.0")).Append(" C\n");
        Bio.Append("Symptoms: ").Append(VisibleSymptomText(in applicant)).Append('\n');
        Bio.Append("Edict suggestion: ").Append(ActionLabel(suggested));
        return Bio.ToString();
    }

    public static float GetLatentChance(OriginRegion origin) => origin switch
    {
        OriginRegion.GreenZone => 0.06f,
        OriginRegion.RedZone => 0.28f,
        _ => 0.12f
    };

    private static void TryApplyLatentDisease(ref ResidentData applicant)
    {
        if (Random.value >= GetLatentChance(applicant.originRegion))
            return;

        ResidentDiseaseRules.Infect(ref applicant, ResidentDiseaseRules.RollRandomDisease());
        applicant.bodyTemperature = ResidentDiseaseRules.NormalBodyTemperature + Random.Range(0.2f, 0.9f);
    }

    private static OriginRegion RollOrigin()
    {
        float roll = Random.value;
        if (roll < 0.30f) return OriginRegion.GreenZone;
        if (roll < 0.80f) return OriginRegion.YellowZone;
        return OriginRegion.RedZone;
    }

    private static FactionType RollFaction()
    {
        float roll = Random.value;
        if (roll < 0.70f) return FactionType.Commoner;
        if (roll < 0.82f) return FactionType.Scholar;
        if (roll < 0.94f) return FactionType.Zealot;
        return FactionType.Aristocrat;
    }

    private static string OriginLabel(OriginRegion origin) => origin switch
    {
        OriginRegion.GreenZone => "GreenZone",
        OriginRegion.RedZone => "RedZone",
        _ => "YellowZone"
    };

    private static string ActionLabel(RuleAction action) => action switch
    {
        RuleAction.Deny => "Deny",
        RuleAction.Quarantine => "Quarantine",
        RuleAction.EscortToDesk => "Escort to desk",
        _ => "Admit"
    };

    private static string VisibleSymptomText(in ResidentData applicant)
    {
        if (applicant.healthStatus == HealthStatus.Incubating
            || applicant.symptoms == SymptomFlags.None)
            return "None apparent";

        if (applicant.HasSymptom(SymptomFlags.Fever)) return "Fever";
        if (applicant.HasSymptom(SymptomFlags.Cough)) return "Cough";
        if (applicant.HasSymptom(SymptomFlags.Fatigue)) return "Fatigue";
        return "Unusual";
    }
}
