public static class ResidentAgingRules
{
    public const byte ElderlyAge = 65;
    public const float BaseDeathChance = 0.05f;
    public const float DeathChancePerYear = 0.03f;
    public const float MaxDeathChance = 0.85f;

    public static float GetOldAgeDeathChance(in ResidentData resident)
    {
        if (!resident.isAlive || resident.GetAgeGroup() != AgeGroupType.Elderly)
            return 0f;

        int yearsPast = resident.age - ElderlyAge;
        if (yearsPast < 0)
            yearsPast = 0;

        float chance = BaseDeathChance + yearsPast * DeathChancePerYear;
        if (resident.hungerMonths > 0)
            chance += 0.05f;
        if (resident.coldMonths > 0)
            chance += 0.05f;
        if (resident.healthStatus == HealthStatus.ActiveInfected)
            chance += 0.08f;
        if (resident.endurance < 30)
            chance += 0.04f;

        return chance > MaxDeathChance ? MaxDeathChance : chance;
    }

    public static bool ShouldRetire(in ResidentData resident) =>
        resident.isAlive
        && resident.GetAgeGroup() == AgeGroupType.Elderly
        && resident.professionType != ProfessionType.None;

    public static void Retire(ref ResidentData resident)
    {
        resident.professionType = ProfessionType.None;
        ResidentAssignmentRules.ClearWorkAssignment(ref resident);
    }

    public static void DieOfAge(ref ResidentData resident)
    {
        resident.isAlive = false;
        resident.professionType = ProfessionType.None;
        ResidentAssignmentRules.ClearHousingAssignment(ref resident);
        ResidentAssignmentRules.ClearWorkAssignment(ref resident);
        ResidentDiseaseRules.ClearDisease(ref resident);
    }
}
