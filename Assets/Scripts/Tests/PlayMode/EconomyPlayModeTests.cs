#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

// ===== Kinh tế hộ gia đình GDD v0.3 mục 6.4 (EC) =====
public class PM_EconomyTests : GameSessionFixture
{
    private const short FakeHouse = 30001;
    private const short FakeWork = 30002;

    private static ResidentData Worker(int id, ProfessionType profession = ProfessionType.Farmer)
    {
        ResidentData r = HealthyApplicant(wealth: 0);
        r.residentID = id;
        r.professionType = profession;
        r.assignedHouseID = FakeHouse;
        r.assignedWorkID = FakeWork;
        r.isAlive = true;
        return r;
    }

    private EconomyReport Run(ResidentData[] residents)
    {
        Data.totalPopulation = residents.Length;   // mẫu = dân thật (hệ số 1)
        return EconomySystem.ProcessMonthly(residents, residents.Length, null, 0, Contain.globalSystemManager);
    }

    [UnityTest]
    public IEnumerator EC01_TraLuong_KhauTruThue_HoTraTienAn()
    {
        Data.treasuryGold = 1000;
        Data.wageLevel = WageLevel.Normal;
        Data.incomeTaxLevel = EntryTaxLevel.Normal;
        Data.currentMonth = 4;   // không phải mùa đông
        ResidentData child = Worker(3);
        child.age = 1;
        child.assignedWorkID = -1;
        var residents = new[] { Worker(1), Worker(2), child };

        EconomyReport r = Run(residents);
        float tax = r.hasTaxOffice ? 0.2f : 0f;
        Assert.AreEqual(16, r.wagesPaid, "2 nông dân × 8");
        Assert.AreEqual(UnityEngine.Mathf.RoundToInt(16 * tax), r.incomeTax);
        Assert.AreEqual(9, r.householdFees, "Tiền ăn 3 × 3 người");
        Assert.AreEqual(1000 - 16 + r.incomeTax + 9, Data.treasuryGold);
        Assert.IsTrue(EconomySystem.TryGetRealWageModifier(1, out float mod) && mod > 0f, "Hộ dư dả -> lương thực tế dương");
        yield return null;
    }

    [UnityTest]
    public IEnumerator EC02_HetVang_NoLuong_HaiThangThiDinhCong_TraNoThiDiLamLai()
    {
        Data.treasuryGold = 0;
        Data.wageLevel = WageLevel.Normal;
        var residents = new[] { Worker(11), Worker(12) };

        EconomyReport r = Run(residents);
        Assert.Greater(residents[0].wageDebt, 0, "Không đủ vàng -> nợ lương");
        Assert.IsFalse(residents[0].IsStriking, "Nợ 1 tháng chưa đình công");

        Data.treasuryGold = 0;
        r = Run(residents);
        Assert.IsTrue(residents[0].IsStriking, "Nợ 2 tháng liên tiếp -> đình công");
        Assert.AreEqual(1f, r.strikeRate, 1e-4f);
        Assert.AreEqual(0f, ResidentProductionRules.GetWorkerPower(in residents[0]), "Đình công: hiệu suất 0");

        Data.treasuryGold = 1000;
        Run(residents);
        Assert.AreEqual(0, residents[0].wageDebt);
        Assert.IsFalse(residents[0].IsStriking, "Trả hết nợ -> đi làm lại");
        yield return null;
    }

    [UnityTest]
    public IEnumerator EC03_HoThieuTien_CapPhatHoacCatKhauPhan()
    {
        Data.treasuryGold = 100;
        ResidentData idle = Worker(21);
        idle.assignedWorkID = -1;   // thất nghiệp, không có Wealth

        Data.feedPolicy = FeedPolicy.Subsidize;
        var a = new[] { idle };
        EconomyReport r = Run(a);
        Assert.AreEqual(3, r.subsidy, "Cấp phát: thành chịu tiền ăn");
        Assert.AreEqual(0, a[0].hungerMonths);

        Data.feedPolicy = FeedPolicy.CutRations;
        var b = new[] { idle };
        b[0].hungerMonths = 0;
        r = Run(b);
        Assert.AreEqual(1, r.cutHouseholds);
        Assert.Greater(b[0].hungerMonths, 0, "Cắt khẩu phần: hộ bị đói dù kho còn");
        yield return null;
    }

    [UnityTest]
    public IEnumerator EC04_LuongThucTe_TheoVidu_GDD()
    {
        // GDD 9.6: hộ 2 người làm + 1 trẻ r = 16 ÷ 9 -> +15; hộ 1 người làm nuôi 2 người r = 8 ÷ 9 -> ≈ −6
        Assert.AreEqual(15f, EconomyRules.GetRealWageModifier(16f / 9f), 1e-3f);
        Assert.AreEqual(-6.33f, EconomyRules.GetRealWageModifier(8f / 9f), 0.01f);
        Assert.AreEqual(-20f, EconomyRules.GetRealWageModifier(0.2f), 1e-3f);
        yield return null;
    }
}
#endif
