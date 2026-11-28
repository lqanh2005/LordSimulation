using System.Text;
using UnityEngine;

public static class ImmigrationRules
{
    // GDD v0.3 mục 4.5: hàng chờ không giới hạn — trần mảng chỉ để tránh tràn bộ nhớ
    public const int MaxWaiting = 160;


    public const int AuthorityRegenPerMonth = 5;          // Điểm Quyền lực hồi mỗi tháng (dùng cho Áp giải thẩm vấn)
    public const int MaxAuthority = 100;
    public const int QuarantineMedicinePerMonth = 1;      // Lương thực đã tính chung với dân số, chỉ thêm thuốc
    public const byte DefaultQuarantineMonths = 1;        // GDD v0.3 mục 4.4: mặc định 1 tháng (trùng thời gian ủ bệnh)

    // ===== Hàng chờ ngoài tường (Waiting Yard) — GDD v0.3 mục 4.5 =====
    // Thành không nuôi người chờ (phát chẩn là Lệnh khẩn cấp — đợt sau). hungerMonths = số tháng đã chờ.
    public const float YardStarveChance = 0.10f;          // Sau 1 tháng: mỗi tháng 10% chết đói
    public const float YardWinterStarveMultiplier = 2f;   // Mùa đông ×2 (gồm cả nguy cơ chết cóng)
    public const int YardLeaveAfterMonths = 2;            // Sau 2 tháng: một phần bỏ đi
    public const float YardLeaveChance = 0.3f;            // Tỉ lệ bỏ đi mỗi tháng (GDD: "một phần" — đề xuất)
    public const float YardBeta = 0.10f;                  // β lây trong hàng chờ mỗi người bệnh (GDD 7.4)

    // ===== Nguồn bệnh từ cổng (GDD 7.4) =====
    public const float SickArrivalChance = 0.10f;          // Người mang bệnh hoặc ủ bệnh
    public const float SickArrivalEpidemicMultiplier = 3f; // ×3 nếu đi qua vùng có dịch (Vùng Đỏ) hoặc tị nạn
    public const float SickArrivalIncubatingShare = 0.6f;  // Phần còn lại đã phát bệnh (phát ban đỏ, nhìn là thấy)

    /// <summary>GDD 4.5: xác suất một người chờ chết đói tháng này (chờ từ 1 tháng trở lên).</summary>
    public static float GetYardStarveChance(int monthsWaited, SeasonType season)
    {
        if (monthsWaited < 1)
            return 0f;
        float chance = YardStarveChance;
        if (season == SeasonType.Winter)
            chance *= YardWinterStarveMultiplier;
        return chance;
    }

    public static int GetEntryTax(in ResidentData applicant, float tariffPercent)
    {
        float percent = Mathf.Max(0f, tariffPercent);
        if (percent <= 0f)
            return 0;
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

    /// <summary>Có vấn đề sức khoẻ nhìn thấy được (phát ban đỏ / triệu chứng).</summary>
    public static bool HasVisibleHealthNote(in ResidentData applicant) =>
        applicant.healthStatus == HealthStatus.ActiveInfected || applicant.symptoms != SymptomFlags.None;

    /// <summary>Nghèo kiệt: không trả nổi thuế nhập thành.</summary>
    public static bool IsDestitute(in ResidentData applicant) => applicant.wealth < StabilityRules.PoorWealthThreshold;

    /// <summary>GDD 9.6: Admit +1 (+2 nếu có note sức khoẻ hoặc Nghèo kiệt).</summary>
    public static float GetAdmitReputationChange(in ResidentData applicant) =>
        HasVisibleHealthNote(in applicant) || IsDestitute(in applicant)
            ? StabilityRules.AdmitNeedyReputation
            : StabilityRules.AdmitReputation;

    /// <summary>GDD 9.6: Deny −1 (−2 nếu có trẻ em).</summary>
    public static float GetDenyReputationChange(in ResidentData applicant, SeasonType season) =>
        applicant.GetAgeGroup() == AgeGroupType.Child
            ? StabilityRules.DenyChildReputation
            : StabilityRules.DenyReputation;

    public static ResidentData CreateApplicant(int residentId) =>
        CreateApplicant(residentId, StabilityRules.DefaultEntryTax, false);

    /// <param name="refugee">Đợt tị nạn thiên tai: tỷ lệ người bệnh và người nghèo cao hơn (GDD 4.1).</param>
    public static ResidentData CreateApplicant(int residentId, EntryTaxLevel entryTax, bool refugee) =>
        CreateApplicant(residentId, entryTax, refugee, true);

    /// <param name="applyDisease">false khi tạo thành viên gia đình — bệnh tính theo cả nhà (FamilyRules.CreateFamily).</param>
    public static ResidentData CreateApplicant(int residentId, EntryTaxLevel entryTax, bool refugee, bool applyDisease)
    {
        GenderType gender = Random.value < 0.5f ? GenderType.Male : GenderType.Female;
        byte age = RollAge();
        OriginRegion origin = RollOrigin();
        if (refugee && origin == OriginRegion.GreenZone)
            origin = OriginRegion.YellowZone;
        FactionType faction = RollFaction();
        byte strength = (byte)Random.Range(20, 81);
        byte endurance = (byte)Random.Range(20, 81);
        byte intellect = (byte)Random.Range(20, 81);
        // GDD 9.6: tỷ lệ người giàu theo thuế nhập thành (Miễn 30% … Hà khắc 5%); tị nạn thì giảm nửa
        float richRate = StabilityRules.GetRichArrivalRate(entryTax) * (refugee ? 0.5f : 1f);
        byte wealth = Random.value < richRate
            ? (byte)Random.Range(40, 81)
            : (byte)Random.Range(refugee ? 0 : 3, 36);
        if (faction == FactionType.Aristocrat)
            wealth = (byte)Mathf.Clamp(wealth + 20, 0, 100);

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
        if (applyDisease)
            TryApplyLatentDisease(ref applicant, refugee);
        return applicant;
    }

    /// <summary>GDD 7.4: 10% mang bệnh / ủ bệnh; ×3 nếu đi qua vùng có dịch (Vùng Đỏ) hoặc đợt tị nạn.</summary>
    public static float GetLatentChance(OriginRegion origin, bool refugee = false)
    {
        float chance = SickArrivalChance;
        if (origin == OriginRegion.RedZone || refugee)
            chance *= SickArrivalEpidemicMultiplier;
        return Mathf.Min(chance, 0.9f);
    }

    private static void TryApplyLatentDisease(ref ResidentData applicant, bool refugee)
    {
        if (Random.value >= GetLatentChance(applicant.originRegion, refugee))
            return;
        ApplyLatentDisease(ref applicant);
    }

    /// <summary>Cho người này mang Sốt Đỏ: 60% đang ủ bệnh (không biểu hiện), còn lại đã phát bệnh.</summary>
    public static void ApplyLatentDisease(ref ResidentData applicant)
    {

        // MVP chỉ có Sốt Đỏ. Người Infected luôn có Phát ban đỏ (nhìn là thấy); Incubating không có biểu hiện.
        ResidentDiseaseRules.Infect(ref applicant, DiseaseType.RedFever);
        if (Random.value >= SickArrivalIncubatingShare)
            ResidentDiseaseRules.ActivateDisease(ref applicant);
        else
            applicant.bodyTemperature = ResidentDiseaseRules.NormalBodyTemperature + Random.Range(0.2f, 0.9f);
    }

    /// <summary>Tuổi nén GDD v0.3: phần lớn là Adult (3–14), một ít trẻ em (0–2) và người già (15–18).</summary>
    private static byte RollAge()
    {
        float roll = Random.value;
        if (roll < 0.15f) return (byte)Random.Range(0, ResidentData.AdultAge);
        if (roll < 0.90f) return (byte)Random.Range(ResidentData.AdultAge, ResidentData.ElderlyAge);
        return (byte)Random.Range(ResidentData.ElderlyAge, ResidentData.ElderlyAge + 4);
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

    public static string OriginLabel(OriginRegion origin) => origin switch
    {
        OriginRegion.GreenZone => "Vùng Xanh",
        OriginRegion.RedZone => "Vùng Đỏ",
        _ => "Vùng Vàng"
    };

    public static string FactionLabel(FactionType faction) => faction switch
    {
        FactionType.Commoner => "Thường dân",
        FactionType.Aristocrat => "Quý tộc",
        FactionType.Scholar => "Học giả",
        FactionType.Zealot => "Cuồng tín",
        _ => "Không"
    };

    public static string ActionLabel(RuleAction action) => action switch
    {
        RuleAction.Deny => "Từ chối",
        RuleAction.Quarantine => "Cách ly",
        RuleAction.ManualReview => "Duyệt tay",
        _ => "Cho vào"
    };

}
