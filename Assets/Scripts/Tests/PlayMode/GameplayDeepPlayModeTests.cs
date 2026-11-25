#if UNITY_INCLUDE_TESTS
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
        Assert.That(d.reputation, Is.InRange(ImmigrationRules.MinReputation, ImmigrationRules.MaxReputation), $"{when}: uy tín ngoài giới hạn");
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
            Assert.That(r.wealth, Is.InRange(0, 100), $"{when}: tài sản cư dân {r.residentID} sai");
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

    [UnityTest]
    public IEnumerator SOAK01_ThanhDuocTiepTe_3Nam_GiuBatBien()
    {
        var rng = new System.Random(12345);
        PlaceAnywhere(BuildingType.House);
        PlaceAnywhere(BuildingType.WaterWell);

        for (int month = 1; month <= 36; month++)
        {
            Data.stockFood = Mathf.Max(Data.stockFood, 300);
            Data.stockCoal = Mathf.Max(Data.stockCoal, 300);
            Data.stockMedicine = Mathf.Max(Data.stockMedicine, 30);
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
        Assert.AreEqual(RuleAction.Admit, action);
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
        Data.riotRiskMeter = 0f;
        Data.treasuryGold = 0;
        Data.stockFood = 1000;
        Data.currentMonth = 3;
        PlaceAnywhere(BuildingType.WaterWell);   // tránh bạo loạn vì khát che mất kết quả
        Data.treasuryGold = 0;

        yield return AdvanceMonths(1);
        Assert.GreaterOrEqual(Data.treasuryGold, 50);
        Assert.Greater(Data.riotRiskMeter, 0f, "Tận thu phải làm dân bất mãn");
    }

    [UnityTest]
    public IEnumerator GT11_ApGiai_TonQuyenLuc_Mot_Lan_Moi_Nguoi()
    {
        GrantResearch(ResearchID.CodeOfLaw);
        Contain.edictManager.CreateRule(Rule("Áp giải", new RuleActionData(RuleActionType.ImmigrationEscort, 10f), Always));
        Data.authorityPoints = 50;

        ResidentData carrier = HealthyApplicant();
        carrier.residentID = 8000;
        ResidentDiseaseRules.Infect(ref carrier, DiseaseType.RedFever);   // đang ủ bệnh, chưa có triệu chứng
        SetSingleApplicant(carrier);   // OpenInspection -> thẩm vấn
        yield return null;
        Assert.AreEqual(40, Data.authorityPoints, "Áp giải tốn 10 quyền lực");

        Contain.immigrationManager.OpenInspection();
        yield return null;
        Assert.AreEqual(40, Data.authorityPoints, "Cùng một người chỉ trả quyền lực 1 lần");

        Data.authorityPoints = 5;
        ResidentData other = HealthyApplicant();
        other.residentID = 8001;
        SetSingleApplicant(other);
        yield return null;
        Assert.AreEqual(5, Data.authorityPoints, "Không đủ quyền lực thì không trừ");
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
    public IEnumerator WY01_NguoiCho_AnLuongThucCuaThanh()
    {
        FillQueue(5, 0);
        Data.stockFood = 1000;
        GameEvents.Listen(EventID.WaitingYardReport, OnReport);
        yield return AdvanceMonths(1);
        GameEvents.Unlisten(EventID.WaitingYardReport, OnReport);

        Assert.IsTrue(_gotReport, "Không có báo cáo hàng chờ");
        Assert.AreEqual(5 * ImmigrationRules.YardFoodPerPerson, _report.foodEaten);
        Assert.AreEqual(0, _report.hungry);
    }

    [UnityTest]
    public IEnumerator WY02_DoiQua2Thang_BoDi_MatUyTin()
    {
        FillQueue(4, 1);   // đã đói 1 tháng
        Data.stockFood = 0;
        Data.reputation = 20f;
        GameEvents.Listen(EventID.WaitingYardReport, OnReport);
        yield return AdvanceMonths(1);
        GameEvents.Unlisten(EventID.WaitingYardReport, OnReport);

        Assert.IsTrue(_gotReport);
        Assert.AreEqual(4, _report.hungry);
        Assert.AreEqual(4, _report.left, "Đói >= 2 tháng thì bỏ đi");
        Assert.Less(Data.reputation, 20f, "Người bỏ đi làm mất uy tín");
    }

    [UnityTest]
    public IEnumerator WY05_HangChoDong_TangBaoLoan()
    {
        FillQueue(ImmigrationRules.MaxWaiting, 0);
        Data.stockFood = 5000;
        GameEvents.Listen(EventID.WaitingYardReport, OnReport);
        yield return AdvanceMonths(1);
        GameEvents.Unlisten(EventID.WaitingYardReport, OnReport);

        Assert.IsTrue(_gotReport);
        Assert.Greater(_report.riotAdded, 0f);
    }
}
#endif
