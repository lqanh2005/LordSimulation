#if UNITY_INCLUDE_TESTS && UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

// ===== Giá thị trường GDD v0.3 mục 6.6 (MK) =====
public class PM_MarketTests : GameSessionFixture
{
    [UnityTest]
    public IEnumerator MK01_KhanHiem_ToiDaCong50PhanTram()
    {
        Data.foodPriceEvent = 1f;
        Data.totalPopulation = 20;
        Data.currentMonth = 4;
        Data.stockFood = 1000;
        Assert.AreEqual(2f, MarketSystem.GetPrice(ResourceType.Food, in Data), 1e-4f, "Đủ kho: giá gốc 2");
        Data.stockFood = 0;
        Assert.AreEqual(3f, MarketSystem.GetPrice(ResourceType.Food, in Data), 1e-4f, "Kho cạn: +50%");
        Data.stockFood = 10;   // nửa mức tiêu thụ 1 tháng
        Assert.AreEqual(2.5f, MarketSystem.GetPrice(ResourceType.Food, in Data), 1e-4f);
        yield return null;
    }

    [UnityTest]
    public IEnumerator MK02_SuKien_BaoTruoc_RoiHoiGia20PhanTramMoiThang()
    {
        Data.marketActiveEvent = MarketEventType.None;
        Data.marketPendingEvent = MarketEventType.CropFailure;
        Data.marketPendingMagnitude = 2f;
        Data.marketPendingInMonths = 2;
        Data.foodPriceEvent = 1f;

        MarketSystem.ProcessMonthly(ref Data);
        Assert.AreEqual(1f, Data.foodPriceEvent, 1e-4f, "Tháng báo trước: giá chưa đổi");
        MarketSystem.ProcessMonthly(ref Data);
        Assert.AreEqual(MarketEventType.CropFailure, Data.marketActiveEvent);
        Assert.AreEqual(2f, Data.foodPriceEvent, 1e-4f);

        Data.marketActiveMonths = 1;   // tháng cuối của sự kiện
        MarketSystem.ProcessMonthly(ref Data);
        Assert.AreEqual(MarketEventType.None, Data.marketActiveEvent);
        Assert.AreEqual(1.8f, Data.foodPriceEvent, 1e-3f, "Hồi về giá gốc 20% phần chênh mỗi tháng");
        yield return null;
    }

    [UnityTest]
    public IEnumerator MK03_TienAnCuaHo_TheoGia()
    {
        Data.foodPriceEvent = 2f;
        Data.stockFood = 100000;
        float ratio = MarketSystem.GetPriceRatio(ResourceType.Food, in Data);
        Assert.AreEqual(2f, ratio, 1e-4f);

        Data.treasuryGold = 0;
        ResidentData idle = HealthyApplicant(wealth: 200);
        idle.assignedHouseID = 30001;
        idle.assignedWorkID = -1;
        var arr = new[] { idle };
        Data.totalPopulation = 1;
        EconomyReport r = EconomySystem.ProcessMonthly(arr, 1, null, 0, Contain.globalSystemManager);
        Assert.AreEqual(UnityEngine.Mathf.RoundToInt(EconomyRules.FoodFeePerPerson * 2f), r.householdFees, "Mất mùa: tiền ăn gấp đôi");
        yield return null;
    }
}
#endif
