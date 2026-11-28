#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// PlayMode – kịch bản sâu theo từng hệ thống (Docs/KichBanTest.md).

// ===== Chạy dài + bất biến =====
public class PM_SoakTests : GameSessionFixture
{
    private void AssertInvariants(string when)
    {
        ref GlobalSystemData d = ref Data;
        Assert.GreaterOrEqual(d.stockFood, 0, $"{when}: lương thực âm");
        Assert.GreaterOrEqual(d.stockCoal, 0, $"{when}: than âm");
        Assert.GreaterOrEqual(d.stockWood, 0, $"{when}: gỗ âm");
        Assert.GreaterOrEqual(d.stockIron, 0, $"{when}: sắt âm");
        Assert.GreaterOrEqual(d.stockMedicine, 0, $"{when}: thuốc âm");
        Assert.That(d.riotRiskMeter, Is.InRange(0f, 100f), $"{when}: bạo loạn ngoài 0–100");
        Assert.That(d.overallHappiness, Is.InRange(0f, 100f), $"{when}: hạnh phúc ngoài 0–100");
        Assert.That(d.reputation, Is.InRange(StabilityRules.ReputationMin, StabilityRules.ReputationMax), $"{when}: uy tín ngoài giới hạn");
        Assert.LessOrEqual(d.authorityPoints, ImmigrationRules.MaxAuthority, $"{when}: quyền lực vượt trần");
        Assert.That(d.currentMonth, Is.InRange(1, 12), $"{when}: tháng sai");
        Assert.GreaterOrEqual(d.totalPopulation, CountAliveResidents(), $"{when}: dân số < số mẫu còn sống");
        Assert.GreaterOrEqual(d.miasma, 0f, $"{when}: Miasma âm");
        Assert.GreaterOrEqual(d.corpsesFresh + d.corpsesRotting, 0, $"{when}: số xác âm");
        Assert.That(Contain.immigrationManager.waitingCount, Is.InRange(0, ImmigrationRules.MaxWaiting), $"{when}: hàng chờ sai");

        var rm = Contain.residentManager;
        for (int i = 0; i < rm.activeCount; i++)
        {
            ref ResidentData r = ref rm.allResidents[i];
            if (!r.isAlive) continue;
            Assert.That(r.happiness, Is.InRange(0f, 1f), $"{when}: hạnh phúc cư dân {r.residentID} sai");
            Assert.That(r.wealth, Is.InRange(0, (int)ResidentSocialRules.MaxWealth), $"{when}: tài sản cư dân {r.residentID} sai");
        }
    }

    /// <summary>Mỗi tháng xử lý người ở Cổng ngẫu nhiên (seed cố định để lặp lại được).</summary>
    private void ProcessGateRandomly(System.Random rng)
    {
        var im = Contain.immigrationManager;
        int n = im.waitingCount;
        for (int i = 0; i < n && im.waitingCount > 0; i++)
        {
            im.OpenInspection();
            if (rng.NextDouble() < 0.6) im.AcceptCurrent();
            else im.DenyCurrent();
        }
    }

    private static readonly BuildingType[] Workplaces =
    {
        BuildingType.Farm, BuildingType.LumberCamp, BuildingType.Mine, BuildingType.GuardPost, BuildingType.Furnace
    };

    [UnityTest]
    public IEnumerator SOAK01_ThanhDuocTiepTe_3Nam_GiuBatBien()
    {
        var rng = new System.Random(12345);
        PlaceAnywhere(BuildingType.House);
        PlaceAnywhere(BuildingType.WaterWell);
        // Infirmary: không có giường cho người Infected thì Riot +5/tháng (GDD 9.6)
        GrantResearch(ResearchID.Medicine);
        PlaceAnywhere(BuildingType.Clinic);

        for (int month = 1; month <= 36; month++)
        {
            Data.stockFood = Mathf.Max(Data.stockFood, 300);
            Data.stockCoal = Mathf.Max(Data.stockCoal, 300);
            Data.stockMedicine = Mathf.Max(Data.stockMedicine, 30);
            // GDD v0.3: Riot theo Happiness — nhận người mà không có nhà thì vô gia cư (−20) kéo Riot lên.
            // "Được tiếp tế đầy đủ" giờ gồm cả nhà ở: mỗi tháng dựng thêm một nhà.
            PlaceAnywhere(BuildingType.House);
            // GDD v0.3 mục 6.4: hộ không có lương thì lương thực tế thấp -> phải có chỗ làm cho dân mới
            PlaceAnywhere(Workplaces[month % Workplaces.Length]);
            ProcessGateRandomly(rng);

            yield return AdvanceMonths(1);
            if (Gameplay.IsIn(GamePlayStateID.Victory))
                Gameplay.ContinueAfterVictory();

            AssertInvariants($"tháng {month}");
            if (Gameplay.IsIn(GamePlayStateID.GameOver))
                Assert.Fail($"Thành được tiếp tế đầy đủ mà vẫn thua ở tháng {month} ({Gameplay.LastGameOverReason})");
        }
    }

    [UnityTest]
    public IEnumerator SOAK02_DeMac_ChayToiKhiThua_GiuBatBien()
    {
        var rng = new System.Random(999);
        Data.stockFood = 20;
        Data.stockCoal = 0;

        for (int month = 1; month <= 48; month++)
        {
            ProcessGateRandomly(rng);
            yield return AdvanceMonths(1);
            if (Gameplay.IsIn(GamePlayStateID.Victory))
                Gameplay.ContinueAfterVictory();

            AssertInvariants($"tháng {month}");
            if (Gameplay.IsIn(GamePlayStateID.GameOver))
                yield break;   // Thua là kết cục hợp lệ — miễn là trước đó không vỡ bất biến nào
        }
    }
}

// ===== Cách ly (GT-07, GT-09) =====
public class PM_QuarantineTests : GameSessionFixture
{
    [UnityTest]
    public IEnumerator RC06_TramCachLy_CanNghienCuuMoiXayDuoc()
    {
        Assert.IsFalse(Placement.IsResearched(BuildingType.QuarantineWard), "Ván mới chưa có Kiểm dịch");
        Assert.AreEqual(0, PlaceAnywhere(BuildingType.QuarantineWard));

        GrantResearch(ResearchID.Quarantine);
        yield return null;
        Assert.IsTrue(Placement.IsResearched(BuildingType.QuarantineWard));
        Assert.AreNotEqual(0, PlaceAnywhere(BuildingType.QuarantineWard));
    }

    [UnityTest]
    public IEnumerator GT07_CachLy3Thang_TonThuoc_RoiRaThanh()
    {
        GrantResearch(ResearchID.Quarantine);
        ushort ward = 0;
        yield return BuildAndComplete(BuildingType.QuarantineWard, id => ward = id);

        SetSingleApplicant(HealthyApplicant());
        Assert.IsTrue(Contain.immigrationManager.HasQuarantineSpace);
        Contain.immigrationManager.QuarantineCurrent();
        yield return null;

        int idx = NewestResidentIndex();
        ResidentData r = Contain.residentManager.GetResidentRef(idx);
        int id = r.residentID;
        Assert.AreEqual(ImmigrationRules.DefaultQuarantineMonths, r.quarantineMonths);
        Assert.AreEqual(ward, (ushort)r.assignedHouseID, "Người cách ly phải ở Trạm");

        // Test này kiểm đếm ngược + cho ra Trạm, không kiểm lây bệnh -> giữ người này khoẻ mỗi tháng
        Data.stockMedicine = 50;
        Data.stockFood = 1000;
        KeepHealthy(id);
        yield return AdvanceMonths(1);
        Assert.Less(GlobalSystem.LastMonthNet.medicine, 0, "Người cách ly phải tốn thuốc");

        for (int m = 0; m < 3; m++)
        {
            Data.stockFood = 1000;
            Data.stockMedicine = 50;
            KeepHealthy(id);
            yield return AdvanceMonths(1);
        }
        KeepHealthy(id);
        Contain.residentManager.RequestReassign();
        yield return null;
        yield return null;

        idx = FindResident(id);
        Assert.GreaterOrEqual(idx, 0);
        ResidentData after = Contain.residentManager.GetResidentRef(idx);
        Assert.IsTrue(after.isAlive, "Người cách ly đã chết");
        Assert.AreEqual(0, after.quarantineMonths);
        Assert.AreNotEqual(ward, (ushort)after.assignedHouseID, "Hết hạn mà khoẻ thì phải rời Trạm");
    }

    private void KeepHealthy(int residentId)
    {
        int i = FindResident(residentId);
        if (i < 0) return;
        ref ResidentData r = ref Contain.residentManager.GetResidentRef(i);
        if (r.isAlive && r.healthStatus != HealthStatus.Healthy)
            ResidentDiseaseRules.ClearDisease(ref r);
    }

    private int FindResident(int residentId)
    {
        var rm = Contain.residentManager;
        for (int i = 0; i < rm.activeCount; i++)
            if (rm.allResidents[i].residentID == residentId)
                return i;
        return -1;
    }
}

// ===== Sắc lệnh trong game thật (ED-02, ED-06, ED-11) + thẩm vấn (GT-11, GT-12) =====
public class PM_EdictGameTests : GameSessionFixture
{
    private static readonly RuleCondition Always = new RuleCondition
    {
        variable = RuleVariable.Population, op = ComparisonOp.GreaterOrEqual, value = 0f
    };

    private static EdictRuleData Rule(string name, RuleActionData then, params RuleCondition[] conditions)
    {
        return new EdictRuleData
        {
            ruleName = name,
            isActive = true,
            conditionCount = (byte)conditions.Length,
            conditions = conditions,
            thenAction = then
        };
    }

    [UnityTest]
    public IEnumerator ED02_ToiDa3SacLenh()
    {
        GrantResearch(ResearchID.CodeOfLaw);
        var em = Contain.edictManager;
        for (int i = 0; i < EdictManager.RuleLimit; i++)
            Assert.GreaterOrEqual(em.CreateRule(Rule($"Luật {i}", new RuleActionData(RuleActionType.ImmigrationAdmit), Always)), 0);
        Assert.AreEqual(-1, em.CreateRule(Rule("Luật thừa", new RuleActionData(RuleActionType.ImmigrationDeny), Always)));
        Assert.AreEqual(EdictManager.RuleLimit, em.activeCount);
        yield return null;
    }

    [UnityTest]
    public IEnumerator ED01_ChuaCoBoLuat_KhongTaoDuoc()
    {
        Assert.AreEqual(-1, Contain.edictManager.CreateRule(Rule("x", new RuleActionData(RuleActionType.ImmigrationDeny), Always)));
        yield return null;
    }

    [UnityTest]
    public IEnumerator ED06_ViSao_GhiDungLuatKhop()
    {
        GrantResearch(ResearchID.CodeOfLaw);
        var poor = new RuleCondition { variable = RuleVariable.ApplicantWealth, op = ComparisonOp.LessThan, value = 10f };
        Contain.edictManager.CreateRule(Rule("Đuổi người nghèo", new RuleActionData(RuleActionType.ImmigrationDeny), poor));

        RuleAction action = Contain.immigrationManager.ExplainSuggestion(HealthyApplicant(wealth: 0), out string reason);
        Assert.AreEqual(RuleAction.Deny, action);
        StringAssert.Contains("Đuổi người nghèo", reason);
        StringAssert.Contains("khớp", reason);

        action = Contain.immigrationManager.ExplainSuggestion(HealthyApplicant(wealth: 80), out reason);
        Assert.AreEqual(RuleAction.ManualReview, action, "GDD 8.4: không khớp rule nào -> duyệt tay");
        StringAssert.Contains("Không sắc lệnh nào khớp", reason);
        yield return null;
    }

    [UnityTest]
    public IEnumerator ED11_SacLenhThemThan_DuVangThiCoHieuLuc()
    {
        GrantResearch(ResearchID.CodeOfLaw);
        Contain.edictManager.CreateRule(Rule("Mua than", new RuleActionData(RuleActionType.AddResourceMonthly, (float)ResourceType.Coal, 20f), Always));
        Data.currentMonth = 3;   // sang tháng 4 (mùa hạ): không đốt than sưởi
        Data.treasuryGold = 1000;
        Data.stockCoal = 0;
        Data.stockFood = 1000;

        yield return AdvanceMonths(1);
        Assert.AreEqual(20, Data.stockCoal, "Đủ vàng: +20 than");
        Assert.LessOrEqual(Data.treasuryGold, 1000 - 40 + 50, "Phải trả 40 vàng (cộng thuế nhỏ của dân)");
    }

    [UnityTest]
    public IEnumerator ED11_SacLenhThemThan_ThieuVangThiKhongHieuLuc()
    {
        GrantResearch(ResearchID.CodeOfLaw);
        Contain.edictManager.CreateRule(Rule("Mua than", new RuleActionData(RuleActionType.AddResourceMonthly, (float)ResourceType.Coal, 20f), Always));
        Data.currentMonth = 3;
        Data.treasuryGold = 0;
        Data.stockCoal = 0;
        Data.stockFood = 1000;

        yield return AdvanceMonths(1);
        Assert.AreEqual(0, Data.stockCoal, "Thiếu vàng: tháng đó sắc lệnh không chạy");
    }

    [UnityTest]
    public IEnumerator ED10_TanThu_TangVangVaBaoLoan()
    {
        GrantResearch(ResearchID.CodeOfLaw);
        Contain.edictManager.CreateRule(Rule("Tận thu", new RuleActionData(RuleActionType.ModifyGoldMonthly, 50f), Always));
        Data.stockFood = 1000;
        Data.currentMonth = 3;
        PlaceAnywhere(BuildingType.WaterWell);   // tránh bạo loạn vì khát che mất kết quả
        Data.treasuryGold = 0;
        Data.riotRiskMeter = 50f;   // giữa thang để công thức Riot GDD 9.6 không bị kẹp ở 0

        yield return AdvanceMonths(1);
        Assert.GreaterOrEqual(Data.treasuryGold, 50);
        float expected = 50f + 50f * EdictRules.TaxRiotPerGold + ResidentSocialSystem.LastRiotDelta;
        Assert.AreEqual(expected, Data.riotRiskMeter, 0.01f, "Tận thu cộng thêm bạo loạn ngoài công thức Happiness");
    }

    [UnityTest]
    public IEnumerator GT11_ManualReview_GateClerkKhongTuXuLy()
    {
        // GDD v0.3 mục 8.3: MANUAL REVIEW đưa gia đình vào hàng duyệt tay
        GrantResearch(ResearchID.CodeOfLaw);
        Contain.edictManager.CreateRule(Rule("Duyệt tay", new RuleActionData(RuleActionType.ImmigrationManualReview), Always));
        SetSingleApplicant(HealthyApplicant(wealth: 20));

        RuleAction action = Contain.immigrationManager.ExplainSuggestion(HealthyApplicant(wealth: 20), out _);
        Assert.AreEqual(RuleAction.ManualReview, action);
        Assert.AreEqual(0, Contain.immigrationManager.ProcessGateClerk(), "MANUAL REVIEW không được Clerk xử lý");
        Assert.AreEqual(1, Contain.immigrationManager.waitingCount);
        yield return null;
    }

    [UnityTest]
    public IEnumerator GC01_GateClerk_TuDuyetToiDa4GiaDinh()
    {
        GrantResearch(ResearchID.CodeOfLaw);
        Contain.edictManager.CreateRule(Rule("Nhận hết", new RuleActionData(RuleActionType.ImmigrationAdmit), Always));
        var im = Contain.immigrationManager;
        for (int i = 0; i < 6; i++)
        {
            ResidentData a = HealthyApplicant(wealth: 20);
            a.residentID = 8100 + i;
            im.waitingApplicants[i] = a;
        }
        im.waitingCount = 6;
        im.RefreshAfterLoad();

        if (!im.ResolveGateClerk())
            Assert.Inconclusive("Không có người lớn nào rảnh việc để làm Gate Clerk");

        int pop = Data.totalPopulation;
        Assert.AreEqual(ImmigrationManager.FamiliesPerClerk, im.ProcessGateClerk());
        Assert.AreEqual(2, im.waitingCount, "Vượt sức Clerk thì để lại duyệt tay");
        Assert.AreEqual(pop + 4, Data.totalPopulation);
        Assert.AreEqual(1, Contain.residentManager.allResidents[NewestResidentIndex()].processedByRule,
            "Ghi lại sắc lệnh nào đã xử lý (Why was this NPC processed?)");
        yield return null;
    }

    [UnityTest]
    public IEnumerator ED12_AndTinhTruocOr()
    {
        // A OR B AND C = A OR (B AND C): A đúng thì khớp dù B AND C sai
        GrantResearch(ResearchID.CodeOfLaw);
        var a = new RuleCondition { variable = RuleVariable.FamilySize, op = ComparisonOp.LessOrEqual, value = 1f };
        var b = new RuleCondition { link = LogicalLink.OR, variable = RuleVariable.FamilySize, op = ComparisonOp.GreaterOrEqual, value = 4f };
        var c = new RuleCondition { link = LogicalLink.AND, variable = RuleVariable.FamilyHasChildOrElderly, op = ComparisonOp.Equal, value = 1f };
        Contain.edictManager.CreateRule(Rule("Ưu tiên", new RuleActionData(RuleActionType.ImmigrationDeny), a, b, c));

        RuleAction action = Contain.immigrationManager.ExplainSuggestion(HealthyApplicant(wealth: 20), out string reason);
        Assert.AreEqual(RuleAction.Deny, action);
        StringAssert.Contains("(", reason, "Giải thích hiện ngoặc tự động");
        yield return null;
    }

    [UnityTest]
    public IEnumerator GT12_QuyenLucHoiMoiThang()
    {
        Data.authorityPoints = 50;
        Data.stockFood = 1000;
        yield return AdvanceMonths(1);
        Assert.AreEqual(50 + ImmigrationRules.AuthorityRegenPerMonth, Data.authorityPoints);

        Data.authorityPoints = (ushort)ImmigrationRules.MaxAuthority;
        yield return AdvanceMonths(1);
        Assert.AreEqual(ImmigrationRules.MaxAuthority, Data.authorityPoints, "Không vượt trần");
    }
}

// ===== Gia đình di cư (GDD v0.3 mục 4.1–4.3) =====
public class PM_FamilyTests : GameSessionFixture
{
    private void SetFamily(int familyId, params ResidentData[] members)
    {
        var im = Contain.immigrationManager;
        for (int i = 0; i < members.Length; i++)
        {
            members[i].familyID = familyId;
            members[i].residentID = 8500 + i;
            im.waitingApplicants[i] = members[i];
        }
        im.waitingCount = members.Length;
        im.RefreshAfterLoad();
        im.OpenInspection();
    }

    [UnityTest]
    public IEnumerator FA01_DotDiCu_DenTheoGiaDinh()
    {
        var im = Contain.immigrationManager;
        im.waitingCount = 0;
        int added = im.EnqueueApplicants(14, EntryTaxLevel.Normal, false);
        Assert.AreEqual(14, added);
        int families = im.CountFamilies();
        Assert.Less(families, 14, "Có gia đình nhiều hơn 1 người");
        for (int i = 0; i < im.waitingCount;)
        {
            ApplicantFamily f = im.GetFamilyAt(i);
            Assert.That(f.count, Is.InRange(1, 5));
            Assert.AreEqual(AgeGroupType.Adult, f.Head.GetAgeGroup(), "Chủ hộ là người lớn");
            for (int k = 1; k < f.count; k++)
                Assert.AreEqual(f.Head.lastNameID, f[k].lastNameID, "Cả nhà chung họ");
            i = f.start + f.count;
        }
        yield return null;
    }

    [UnityTest]
    public IEnumerator FA02_ChoVao_CaNhaVaoThanh_CongMotUyTin()
    {
        Data.reputation = 50f;
        SetFamily(77, HealthyApplicant(wealth: 20), HealthyApplicant(wealth: 20), HealthyApplicant(wealth: 20));
        int pop = Data.totalPopulation;
        Contain.immigrationManager.AcceptCurrent();
        yield return null;
        Assert.AreEqual(pop + 3, Data.totalPopulation);
        Assert.AreEqual(0, Contain.immigrationManager.waitingCount);
        Assert.AreEqual(50f + StabilityRules.AdmitReputation, Data.reputation, 1e-4f, "Mỗi gia đình +1, không phải mỗi người");
    }

    [UnityTest]
    public IEnumerator FA03_TuChoiNhaCoTreEm_TruHai()
    {
        Data.reputation = 50f;
        ResidentData kid = HealthyApplicant(wealth: 20);
        kid.age = 1;
        SetFamily(78, HealthyApplicant(wealth: 20), kid);
        Contain.immigrationManager.DenyCurrent();
        yield return null;
        Assert.AreEqual(50f + StabilityRules.DenyChildReputation, Data.reputation, 1e-4f);
        Assert.AreEqual(0, Contain.immigrationManager.waitingCount, "Từ chối cả nhà");
    }

    [UnityTest]
    public IEnumerator FA04_NoteTheoCapHoSo()
    {
        ResidentData r = HealthyApplicant(wealth: 20);
        r.originRegion = OriginRegion.RedZone;
        r.rolledNotes = (byte)NoteFlags.Cough;
        Assert.AreEqual(NoteFlags.None, FamilyRules.GetVisibleNotes(in r, FamilyRules.ProfileLevelBasic), "Cấp 0 chưa thấy Ho / vùng dịch");
        NoteFlags lv1 = FamilyRules.GetVisibleNotes(in r, FamilyRules.ProfileLevelExam);
        Assert.AreEqual(NoteFlags.Cough | NoteFlags.PlagueRoute, lv1);

        ResidentDiseaseRules.Infect(ref r, DiseaseType.RedFever);
        ResidentDiseaseRules.ActivateDisease(ref r);
        Assert.IsTrue((FamilyRules.GetVisibleNotes(in r, FamilyRules.ProfileLevelBasic) & NoteFlags.Rash) != 0,
            "Infected luôn có Phát ban đỏ từ cấp 0");
        yield return null;
    }
}

// ===== Hàng chờ ở Cổng (WY) =====
public class PM_WaitingYardTests : GameSessionFixture
{
    private WaitingYardReportPayload _report;
    private bool _gotReport;

    private void OnReport(object p)
    {
        if (p is WaitingYardReportPayload r) { _report = r; _gotReport = true; }
    }

    private void FillQueue(int count, byte hungerMonths)
    {
        var im = Contain.immigrationManager;
        for (int i = 0; i < count; i++)
        {
            ResidentData a = HealthyApplicant();
            a.residentID = 7000 + i;
            a.hungerMonths = hungerMonths;
            im.waitingApplicants[i] = a;
        }
        im.waitingCount = count;
        im.RefreshAfterLoad();
    }

    [UnityTest]
    public IEnumerator WY01_NguoiCho_KhongAnLuongThucCuaThanh()
    {
        // GDD v0.3 mục 4.5: thành không nuôi người chờ; chờ qua 1 tháng là đói
        FillQueue(5, 0);
        Data.stockFood = 1000;
        GameEvents.Listen(EventID.WaitingYardReport, OnReport);
        yield return AdvanceMonths(1);
        GameEvents.Unlisten(EventID.WaitingYardReport, OnReport);

        Assert.IsTrue(_gotReport, "Không có báo cáo hàng chờ");
        Assert.AreEqual(0, _report.foodEaten);
        Assert.AreEqual(_report.waiting, _report.hungry, "Ai còn chờ sau 1 tháng đều đói");
    }

    [UnityTest]
    public IEnumerator WY02_DoiQua2Thang_BoDi_MatUyTin()
    {
        FillQueue(20, 2);   // đã chờ 2 tháng
        Data.stockFood = 0;
        Data.reputation = 20f;   // dưới 50: kéo về 50 làm tăng ~3, người chết/bỏ đi phải kéo xuống hơn thế
        GameEvents.Listen(EventID.WaitingYardReport, OnReport);
        yield return AdvanceMonths(1);
        GameEvents.Unlisten(EventID.WaitingYardReport, OnReport);

        Assert.IsTrue(_gotReport);
        Assert.Greater(_report.left, 0, "Chờ >= 2 tháng thì một phần bỏ đi");
        float expected = 20f + (50f - 20f) * StabilityRules.ReputationPullRate
            + _report.left * StabilityRules.YardLeaveReputation + _report.died * StabilityRules.YardDeathReputation;
        Assert.AreEqual(expected, Data.reputation, 1e-3f, "Mỗi người bỏ đi −1, chết −2");
    }

    [UnityTest]
    public IEnumerator WY05_HangChoDong_KhongCongThangBaoLoan()
    {
        // GDD v0.3 mục 9.6: Riot chỉ theo công thức Happiness / đình công / Infirmary; hàng chờ hại qua Reputation + Miasma
        FillQueue(ImmigrationRules.MaxWaiting, 0);
        Data.stockFood = 5000;
        GameEvents.Listen(EventID.WaitingYardReport, OnReport);
        yield return AdvanceMonths(1);
        GameEvents.Unlisten(EventID.WaitingYardReport, OnReport);

        Assert.IsTrue(_gotReport);
        Assert.AreEqual(0f, _report.riotAdded);
    }
}
#endif
