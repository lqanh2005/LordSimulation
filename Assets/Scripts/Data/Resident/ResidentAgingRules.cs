public static class ResidentAgingRules
{
    // GDD v0.3 mục 5.4: Elderly từ 15 tuổi (tuổi nén), mỗi THÁNG có nguy cơ chết già tăng dần theo tuổi
    public const byte ElderlyAge = ResidentData.ElderlyAge;
    public const float BaseDeathChance = 0.01f;       // / tháng khi vừa thành Elderly
    public const float DeathChancePerYear = 0.01f;    // + mỗi năm sau 15
    public const float MaxDeathChance = 0.3f;

    public static float GetOldAgeDeathChance(in ResidentData resident)
    {
        if (!resident.isAlive || resident.GetAgeGroup() != AgeGroupType.Elderly)
            return 0f;

        int yearsPast = resident.age - ElderlyAge;
        if (yearsPast < 0)
            yearsPast = 0;

        float chance = BaseDeathChance + yearsPast * DeathChancePerYear;
        // Người già dễ chết hơn khi đói / lạnh (chết vì bệnh tính riêng ở ResidentDiseaseSystem)
        if (resident.hungerMonths > 0 || resident.thirstMonths > 0)
            chance *= 1.5f;
        if (resident.coldMonths > 0)
            chance *= 1.5f;

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
