#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

// ===== Lệnh khẩn cấp GDD v0.3 mục 9.4 (EM) =====
public class PM_EmergencyTests : GameSessionFixture
{
    [UnityTest]
    public IEnumerator EM01_ThietQuanLuat_RiotGiamNgay_LanSauChiCon15()
    {
        Data.riotRiskMeter = 80f;
        EmergencyOrders.Offer(EmergencyEvent.RiotImminent);
        Assert.AreEqual(EmergencyEvent.RiotImminent, EmergencyOrders.State.openEvent);
        EmergencyOrders.Choose(EmergencyOrder.MartialLaw);
        Assert.AreEqual(50f, Data.riotRiskMeter, 1e-3f, "Riot −30 ngay");
        Assert.AreEqual(EmergencyOrders.MartialLawMonths, EmergencyOrders.State.martialLawMonths, "Happiness −10 trong 3 tháng");

        Data.riotRiskMeter = 80f;
        EmergencyOrders.Choose(EmergencyOrder.MartialLaw);
        Assert.AreEqual(65f, Data.riotRiskMeter, 1e-3f, "Lần sau dùng chỉ còn Riot −15");
        yield return null;
    }

    [UnityTest]
    public IEnumerator EM02_TienLe_GiaHappinessTang50PhanTram()
    {
        EmergencyOrders.Choose(EmergencyOrder.DoubleShift);
        Assert.AreEqual(EmergencyOrders.DoubleShiftMood, EmergencyOrders.State.minersMood, 1e-3f);
        Assert.AreEqual(1.5f, EmergencyOrders.GetProductionMultiplier(BuildingType.Mine), 1e-3f, "Than +50%");
        EmergencyOrders.Choose(EmergencyOrder.DoubleShift);
        Assert.AreEqual(EmergencyOrders.DoubleShiftMood * 1.5f, EmergencyOrders.State.minersMood, 1e-3f, "Dùng lại: giá +50%");
        yield return null;
    }

    [UnityTest]
    public IEnumerator EM03_DongCong_ChanChoVao_TruUyTin()
    {
        Data.reputation = 50f;
        EmergencyOrders.Choose(EmergencyOrder.CloseGate);
        Assert.IsTrue(EmergencyOrders.GateClosed);
        Assert.AreEqual(50f + EmergencyOrders.CloseGateReputation, Data.reputation, 1e-3f);

        SetSingleApplicant(HealthyApplicant(wealth: 20));
        Contain.immigrationManager.AcceptCurrent();
        Assert.AreEqual(1, Contain.immigrationManager.waitingCount, "Cổng đóng: không cho vào");
        Contain.immigrationManager.DenyCurrent();
        Assert.AreEqual(0, Contain.immigrationManager.waitingCount, "Vẫn từ chối được");
        yield return null;
    }

    [UnityTest]
    public IEnumerator EM04_DotXacTapThe_XoaThiTheVaMiasma()
    {
        Data.reputation = 50f;
        Data.corpsesFresh = 5;
        Data.corpsesRotting = 3;
        Data.miasma = 3f;
        EmergencyOrders.Choose(EmergencyOrder.MassCremation);
        Assert.AreEqual(0, Data.corpsesFresh + Data.corpsesRotting);
        Assert.AreEqual(0f, Data.miasma);
        Assert.AreEqual(50f + EmergencyOrders.CremationReputation, Data.reputation, 1e-3f);
        yield return null;
    }

    [UnityTest]
    public IEnumerator EM05_HetThangChuaChon_KhongLamGi()
    {
        EmergencyOrders.Offer(EmergencyEvent.CoalRunningOut);
        Data.stockFood = 5000;
        yield return AdvanceMonths(1);
        Assert.IsFalse(EmergencyOrders.BurnWoodActive, "Không chọn -> Không làm gì");
        Assert.AreNotEqual(EmergencyEvent.CoalRunningOut, EmergencyOrders.State.openEvent);
    }

    [UnityTest]
    public IEnumerator EM06_DonNguoi_NhaGoChua150()
    {
        var house = new BuildingData { buildingType = BuildingType.House, maxOccupancy = 6 };
        Assert.AreEqual(6, EmergencyOrders.GetHousingCapacity(in house));
        EmergencyOrders.Choose(EmergencyOrder.CrowdHouses);
        Assert.AreEqual(9, EmergencyOrders.GetHousingCapacity(in house));
        var tent = new BuildingData { buildingType = BuildingType.Tent, maxOccupancy = 3 };
        Assert.AreEqual(3, EmergencyOrders.GetHousingCapacity(in tent), "Lều không chứa thêm");
        yield return null;
    }

    [UnityTest]
    public IEnumerator EM07_DotGo_ThayThan()
    {
        EmergencyOrders.Choose(EmergencyOrder.BurnWood);
        Data.currentMonth = 10;   // sang tháng 11: mùa đông
        Data.stockFood = 5000;
        Data.stockCoal = 0;
        Data.stockWood = 10000;
        yield return AdvanceMonths(1);
        Assert.Less(Data.stockWood, 10000, "Đốt gỗ thay than");
    }
}
#endif
