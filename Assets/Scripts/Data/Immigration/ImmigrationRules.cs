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
    public const int AuthorityRegenPerMonth = 5;          // Điểm Quyền lực hồi mỗi tháng (dùng cho Áp giải thẩm vấn)
    public const int MaxAuthority = 100;
    public const float MinArrivalMultiplier = 0.25f;      // Uy tín tệ nhất vẫn còn ~1/4 lượng người tới
    public const float ReputationRecoveryPerMonth = 0.5f; // Uy tín âm tự hồi dần về 0 (tiếng xấu phai theo thời gian)
    public const int QuarantineMedicinePerMonth = 1;      // Lương thực đã tính chung với dân số, chỉ thêm thuốc
    public const byte DefaultQuarantineMonths = 3;        // GDD 3.1.6: mặc định 3 tháng (balance TBD)
    public const float DenySickReputationGain = 0.5f;     // Từ chối người có triệu chứng rõ
    public const float DenyHealthyReputationLoss = 1f;    // Từ chối người khoẻ (trước hệ số)

    // ===== Áp lực hàng chờ (Waiting Yard) — GDD 3.1.7 =====
    public const int YardFoodPerPerson = 1;               // Lương thực / người chờ / tháng (lấy từ kho thành)
    public const float YardHungerHappinessPenalty = 0.1f;
    public const float YardSickDeathMultiplier = 2f;      // Phát bệnh trong hàng chờ: không ai chữa -> chết gấp đôi
    public const float YardSickHungryDeathBonus = 0.05f;  // Thêm nếu đang đói
    public const int YardLeaveHungerMonths = 2;           // Đói >= N tháng thì bỏ đi
    public const float YardLeaveReputationPenalty = 0.5f; // Mỗi người bỏ đi vì đói
    public const float YardInfectChancePerCarrier = 0.05f;
    public const float YardMaxInfectChance = 0.5f;
    public const float YardCrowdThreshold = 0.5f;         // Đầy hơn 50% sức chứa thì bắt đầu bất ổn
    public const float YardMaxCrowdRiot = 4f;             // Bạo loạn tối đa / tháng khi hàng chờ đầy
    public const float YardHungryRiotPerPerson = 0.15f;

    /// <summary>Bạo loạn sinh ra từ hàng chờ trong tháng.</summary>
    public static float GetYardRiot(int waiting, int hungry)
    {
        float crowd = MaxWaiting > 0 ? (float)waiting / MaxWaiting : 0f;
        float riot = crowd > YardCrowdThreshold
            ? (crowd - YardCrowdThreshold) / (1f - YardCrowdThreshold) * YardMaxCrowdRiot
            : 0f;
        return riot + hungry * YardHungryRiotPerPerson;
    }

    /// <summary>Xác suất một người khỏe bị lây trong hàng chờ (đông + nhiều người bệnh thì cao).</summary>
    public static float GetYardInfectChance(int carriers, int waiting)
    {
        if (carriers <= 0)
            return 0f;
        float crowdMult = 1f + (MaxWaiting > 0 ? (float)waiting / MaxWaiting : 0f);
        return Mathf.Min(YardMaxInfectChance, carriers * YardInfectChancePerCarrier * crowdMult);
    }

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

        // Uy tín thấp làm ít người tới, nhưng không bao giờ tắt hẳn dòng người (tránh kẹt game:
        // không ai tới thì cũng không có cách nào lấy lại uy tín ở Cổng)
        float mult = 0.5f + Mathf.Clamp(reputation, MinReputation, MaxReputation) / 100f;
        if (mult < MinArrivalMultiplier)
            mult = MinArrivalMultiplier;

        float expected = count * mult;
        int scaled = Mathf.FloorToInt(expected);
        if (Random.value < expected - scaled)   // làm tròn ngẫu nhiên giữ đúng kỳ vọng
            scaled++;
        return scaled;
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

    /// <summary>
    /// GDD 3.1.5 + Pillar 1: từ chối giữ thành an toàn nhưng mất uy tín, tuỳ sức khoẻ / nghề / nguồn gốc / khủng hoảng.
    /// - Có triệu chứng rõ ràng: thiên hạ thông cảm → cộng nhẹ.
    /// - Người khoẻ: trừ uy tín; người có nghề quý, quý tộc, giàu có bị từ chối thì tiếng xấu lan xa hơn.
    /// - Dân vùng đỏ bị nghi kỵ sẵn → trừ ít hơn. Mùa đông (đuổi người ra giữa trời rét) → trừ nặng hơn.
    /// </summary>
    public static float GetDenyReputationChange(in ResidentData applicant, SeasonType season)
    {
        bool visiblySick = applicant.healthStatus == HealthStatus.ActiveInfected
            || applicant.symptoms != SymptomFlags.None;
        if (visiblySick)
            return DenySickReputationGain;

        float loss = DenyHealthyReputationLoss + applicant.wealth / 50f;
        if (applicant.professionType == ProfessionType.Doctor || applicant.professionType == ProfessionType.Teacher)
            loss += 0.5f;
        if (applicant.factionType == FactionType.Aristocrat)
            loss += 1f;
        if (applicant.originRegion == OriginRegion.RedZone)
            loss *= 0.5f;
        if (season == SeasonType.Winter)
            loss *= 1.5f;
        return -loss;
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

    /// <summary>Hồ sơ người xin vào hiển thị ở Cổng. Thân nhiệt chỉ đo được khi đã có Nhiệt kế (GDD Tier 1).</summary>
    public static string FormatProfile(in ResidentData applicant, RuleAction suggested, string edictReason, bool hasThermometer)
    {
        Bio.Clear();
        Bio.Append("<b>").Append(NameDatabase.GetFullName(applicant.firstNameID, applicant.lastNameID)).Append("</b>");
        Bio.Append(applicant.gender == GenderType.Female ? "  (Nữ)\n" : "  (Nam)\n");
        Bio.Append("Tuổi: ").Append(applicant.age);
        Bio.Append("   Phe phái: ").Append(FactionLabel(applicant.factionType)).Append('\n');
        Bio.Append("Xuất thân: ").Append(OriginLabel(applicant.originRegion));
        Bio.Append("   Nghề: ").Append(ResidentInfoPopup.GetProfessionName(applicant.professionType)).Append('\n');
        Bio.Append("Tài sản: ").Append(applicant.wealth);
        Bio.Append("   Hạnh phúc: ").Append((applicant.happiness * 100f).ToString("0")).Append("%\n");
        Bio.Append("Sức mạnh ").Append(applicant.strength);
        Bio.Append("  Bền ").Append(applicant.endurance);
        Bio.Append("  Trí ").Append(applicant.intellect).Append('\n');
        if (hasThermometer)
        {
            bool fever = applicant.bodyTemperature >= 37.5f;
            Bio.Append("Thân nhiệt: ");
            if (fever) Bio.Append("<color=#E74C3C>");
            Bio.Append(applicant.bodyTemperature.ToString("0.0")).Append(" °C");
            if (fever) Bio.Append("</color>");
            Bio.Append('\n');
        }
        else
        {
            Bio.Append("Thân nhiệt: <i>chưa đo được (cần nghiên cứu Nhiệt kế)</i>\n");
        }
        Bio.Append("Triệu chứng: ").Append(VisibleSymptomText(in applicant)).Append('\n');
        Bio.Append("Gợi ý: <b>").Append(ActionLabel(suggested)).Append("</b>");
        if (!string.IsNullOrEmpty(edictReason))
            Bio.Append("\n<size=13><i>Vì sao: ").Append(edictReason).Append("</i></size>");
        return Bio.ToString();
    }

    /// <summary>
    /// Hồ sơ dạng bảng (nhãn, giá trị) cho màn Cổng kiểu mockup. Tên hiển thị riêng ở thẻ tên;
    /// gợi ý + "Vì sao" hiển thị riêng ở cuối bảng.
    /// </summary>
    public static void AppendProfileRows(in ResidentData applicant, bool hasThermometer, System.Collections.Generic.List<(string label, string value)> rows)
    {
        rows.Add(("Tuổi:", applicant.age.ToString()));
        rows.Add(("Giới tính:", applicant.gender == GenderType.Female ? "Nữ" : "Nam"));
        rows.Add(("Nghề nghiệp:", ResidentInfoPopup.GetProfessionName(applicant.professionType)));
        rows.Add(("Xuất thân:", OriginLabel(applicant.originRegion)));
        rows.Add(("Phe phái:", FactionLabel(applicant.factionType)));
        rows.Add(("Tài sản:", applicant.wealth.ToString()));
        rows.Add(("Thể chất:", $"Sức {applicant.strength} · Bền {applicant.endurance} · Trí {applicant.intellect}"));

        if (hasThermometer)
        {
            string temp = applicant.bodyTemperature.ToString("0.0") + " °C";
            rows.Add(("Thân nhiệt:", applicant.bodyTemperature >= 37.5f ? $"<color=#E86A5A>{temp}</color>" : temp));
        }
        else
        {
            rows.Add(("Thân nhiệt:", "<i>cần nghiên cứu Nhiệt kế</i>"));
        }

        rows.Add(("Triệu chứng:", VisibleSymptomText(in applicant)));
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
        RuleAction.EscortToDesk => "Áp giải tới bàn thẩm vấn",
        _ => "Cho vào"
    };

    private static string VisibleSymptomText(in ResidentData applicant)
    {
        if (applicant.healthStatus == HealthStatus.Incubating
            || applicant.symptoms == SymptomFlags.None)
            return "Không thấy biểu hiện";

        if (applicant.HasSymptom(SymptomFlags.Fever)) return "<color=#E74C3C>Sốt</color>";
        if (applicant.HasSymptom(SymptomFlags.Cough)) return "<color=#E74C3C>Ho</color>";
        if (applicant.HasSymptom(SymptomFlags.Fatigue)) return "<color=#E67E22>Mệt mỏi</color>";
        return "<color=#E67E22>Bất thường</color>";
    }
}
