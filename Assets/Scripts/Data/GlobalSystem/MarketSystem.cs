using UnityEngine;

/// <summary>Sự kiện thị trường MVP (GDD v0.3 mục 6.6).</summary>
public enum MarketEventType : byte { None = 0, CropFailure = 1, BumperHarvest = 2, Blizzard = 3 }

/// <summary>
/// Giá thị trường GDD v0.3 mục 6.6: Giá = Giá gốc × Hệ số khan hiếm × Hệ số sự kiện. Người chơi không tự đặt giá.
/// - Khan hiếm: kho dưới mức tiêu thụ 1 tháng thì giá tăng dần, tối đa +50%.
/// - Sự kiện: Mất mùa vùng lân cận (Food ×1,5–2, 3 tháng), Được mùa (Food ×0,7, 2 tháng), Bão tuyết (Coal, Wood ×2 suốt cơn bão).
/// - Hồi giá: hết sự kiện, mỗi tháng kéo về giá gốc 20% phần chênh.
/// - Báo trước: thông báo chính thức 1 tháng trước (chắc chắn); tin đồn qua Cổng 2 tháng trước (≈70% đúng).
/// Trạng thái lưu trong GlobalSystemData (save v15).
/// </summary>
public static class MarketSystem
{
    // ===== Giá gốc (tạm) =====
    public static float GetBasePrice(ResourceType type) => type switch
    {
        ResourceType.Food => 2f,
        ResourceType.Wood => 2f,
        ResourceType.Coal => 3f,
        ResourceType.Medicine => 8f,
        ResourceType.Iron => 4f,
        _ => 1f
    };

    public const float MaxScarcityBonus = 0.5f;
    public const float RecoveryRate = 0.2f;

    public const float CropFailureChance = 0.06f;      // Mỗi tháng (khi chưa có sự kiện lương thực) — GDD TBD
    public const float BumperChance = 0.06f;
    public static readonly Vector2 CropFailureMagnitude = new Vector2(1.5f, 2f);
    public const float BumperMagnitude = 0.7f;
    public const byte CropFailureMonths = 3;
    public const byte BumperMonths = 2;
    public const float BlizzardMagnitude = 2f;

    public const byte RumorLeadMonths = 2;             // Tin đồn qua cổng: 2 tháng trước
    public const float FakeRumorChance = 0.05f;        // ≈ 30% tin đồn là sai
    public const float RumorNoteChance = 0.4f;         // Người di cư tới trong lúc có tin đồn mang note "Tin đồn thị trường"

    /// <summary>Lượng tiêu thụ 1 tháng của thành (để tính khan hiếm).</summary>
    public static float GetMonthlyNeed(ResourceType type, in GlobalSystemData d) => type switch
    {
        ResourceType.Food => d.totalPopulation * GlobalSystemManager.GetFoodPerCapita(d.CurrentSeason),
        ResourceType.Coal => d.CurrentSeason == SeasonType.Winter ? d.totalPopulation : 0f,
        ResourceType.Medicine => d.totalInfected,
        _ => 0f
    };

    public static int GetStock(ResourceType type, in GlobalSystemData d) => type switch
    {
        ResourceType.Food => d.stockFood,
        ResourceType.Wood => d.stockWood,
        ResourceType.Coal => d.stockCoal,
        ResourceType.Medicine => d.stockMedicine,
        ResourceType.Iron => d.stockIron,
        _ => 0
    };

    /// <summary>Kho dưới mức tiêu thụ 1 tháng -> giá tăng dần, tối đa +50% khi kho cạn.</summary>
    public static float GetScarcityMultiplier(ResourceType type, in GlobalSystemData d)
    {
        float need = GetMonthlyNeed(type, in d);
        if (need <= 0f)
            return 1f;
        float stock = GetStock(type, in d);
        if (stock >= need)
            return 1f;
        return 1f + MaxScarcityBonus * (1f - stock / need);
    }

    public static float GetEventMultiplier(ResourceType type, in GlobalSystemData d) => type switch
    {
        ResourceType.Food => d.foodPriceEvent > 0f ? d.foodPriceEvent : 1f,
        ResourceType.Coal => d.coalPriceEvent > 0f ? d.coalPriceEvent : 1f,
        ResourceType.Wood => d.woodPriceEvent > 0f ? d.woodPriceEvent : 1f,
        _ => 1f
    };

    public static float GetPrice(ResourceType type, in GlobalSystemData d) =>
        GetBasePrice(type) * GetScarcityMultiplier(type, in d) * GetEventMultiplier(type, in d);

    /// <summary>Giá hiện tại ÷ giá gốc (dùng cho tiền ăn / sưởi của hộ).</summary>
    public static float GetPriceRatio(ResourceType type, in GlobalSystemData d) => GetPrice(type, in d) / GetBasePrice(type);

    // ===== Theo tháng (GlobalSystemManager gọi sau khi cập nhật thời tiết) =====
    public static void ProcessMonthly(ref GlobalSystemData d)
    {
        // 1. Sự kiện lương thực đang diễn ra / hồi giá
        if (d.marketActiveMonths > 0)
        {
            d.marketActiveMonths--;
            if (d.marketActiveMonths == 0)
                d.marketActiveEvent = MarketEventType.None;
        }
        if (d.marketActiveEvent == MarketEventType.None)
            d.foodPriceEvent = Recover(d.foodPriceEvent);
        else
            d.foodPriceEvent = d.marketActiveMagnitude;

        // 2. Sự kiện đã lên lịch tới hạn -> bắt đầu
        if (d.marketPendingEvent != MarketEventType.None && d.marketPendingInMonths > 0)
        {
            d.marketPendingInMonths--;
            if (d.marketPendingInMonths == 1)
                Announce(d.marketPendingEvent, d.marketPendingMagnitude, official: true);
            if (d.marketPendingInMonths == 0)
            {
                d.marketActiveEvent = d.marketPendingEvent;
                d.marketActiveMagnitude = d.marketPendingMagnitude;
                d.marketActiveMonths = d.marketPendingEvent == MarketEventType.CropFailure ? CropFailureMonths : BumperMonths;
                d.foodPriceEvent = d.marketActiveMagnitude;
                d.marketPendingEvent = MarketEventType.None;
                NotificationFeed.Show($"[Tin thị trường] {EventLabel(d.marketActiveEvent)}: giá lương thực ×{d.marketActiveMagnitude:0.#} trong {d.marketActiveMonths} tháng",
                    NoticeLevel.Info, "market-event", TreasuryWindow.Open);
            }
        }

        // 3. Bão tuyết: than, gỗ ×2 suốt cơn bão, hết bão thì hồi dần
        if (d.currentWeather == WeatherEvent.Blizzard)
        {
            d.coalPriceEvent = BlizzardMagnitude;
            d.woodPriceEvent = BlizzardMagnitude;
        }
        else
        {
            d.coalPriceEvent = Recover(d.coalPriceEvent);
            d.woodPriceEvent = Recover(d.woodPriceEvent);
        }

        // 4. Lên lịch sự kiện mới 2 tháng sau (kèm tin đồn), hoặc tin đồn sai
        if (d.rumorInMonths > 0)
            d.rumorInMonths--;
        if (d.marketActiveEvent == MarketEventType.None && d.marketPendingEvent == MarketEventType.None)
        {
            float roll = Random.value;
            if (roll < CropFailureChance)
                Schedule(ref d, MarketEventType.CropFailure, Random.Range(CropFailureMagnitude.x, CropFailureMagnitude.y));
            else if (roll < CropFailureChance + BumperChance)
                Schedule(ref d, MarketEventType.BumperHarvest, BumperMagnitude);
            else if (d.rumorInMonths == 0 && Random.value < FakeRumorChance)
            {
                d.rumorEvent = Random.value < 0.5f ? MarketEventType.CropFailure : MarketEventType.BumperHarvest;
                d.rumorInMonths = RumorLeadMonths;
                d.rumorTrue = false;
            }
        }
    }

    private static void Schedule(ref GlobalSystemData d, MarketEventType type, float magnitude)
    {
        d.marketPendingEvent = type;
        d.marketPendingMagnitude = magnitude;
        d.marketPendingInMonths = RumorLeadMonths;
        d.rumorEvent = type;
        d.rumorInMonths = RumorLeadMonths;
        d.rumorTrue = true;
    }

    private static float Recover(float mult)
    {
        if (mult <= 0f)
            return 1f;
        float next = mult + (1f - mult) * RecoveryRate;
        return Mathf.Abs(next - 1f) < 0.02f ? 1f : next;
    }

    public static string EventLabel(MarketEventType type) => type switch
    {
        MarketEventType.CropFailure => "Mất mùa ở vùng lân cận",
        MarketEventType.BumperHarvest => "Được mùa",
        MarketEventType.Blizzard => "Bão tuyết",
        _ => "Không có"
    };

    /// <summary>Thông báo chính thức 1 tháng trước sự kiện (loại Tin thị trường).</summary>
    public static void Announce(MarketEventType type, float magnitude, bool official)
    {
        string what = type switch
        {
            MarketEventType.CropFailure => "Sương giá phía Bắc: giá lương thực dự kiến tăng mạnh từ tháng sau",
            MarketEventType.BumperHarvest => "Vùng lân cận được mùa: giá lương thực dự kiến giảm từ tháng sau",
            MarketEventType.Blizzard => "Bão tuyết sắp tới: giá than và gỗ dự kiến tăng gấp đôi từ tháng sau — hãy tích trữ",
            _ => null
        };
        if (what != null)
            NotificationFeed.Show($"[Tin thị trường] {what}", type == MarketEventType.Blizzard ? NoticeLevel.Warning : NoticeLevel.Info,
                "market-forecast", TreasuryWindow.Open);
    }

    /// <summary>Lời tin đồn người di cư kể ở Cổng (có thể sai).</summary>
    public static string DescribeRumor(in GlobalSystemData d)
    {
        if (d.rumorInMonths == 0 || d.rumorEvent == MarketEventType.None)
            return null;
        return d.rumorEvent == MarketEventType.CropFailure
            ? $"nghe nói vùng lân cận sắp mất mùa — lương thực sẽ đắt (khoảng {d.rumorInMonths} tháng nữa)"
            : $"nghe nói vùng lân cận được mùa — lương thực sẽ rẻ (khoảng {d.rumorInMonths} tháng nữa)";
    }

    /// <summary>Một dòng giá cho UI: "Lương thực 3,0 (▲50%)".</summary>
    public static string DescribePrice(ResourceType type, in GlobalSystemData d)
    {
        float price = GetPrice(type, in d);
        float baseP = GetBasePrice(type);
        float pct = (price / baseP - 1f) * 100f;
        string trend = Mathf.Abs(pct) < 1f ? "" : pct > 0f ? $" <color=#E86A5A>▲{pct:0}%</color>" : $" <color=#5CCB6A>▼{-pct:0}%</color>";
        return $"{MerchantManager.GetResourceName(type)} {price:0.0}{trend}";
    }
}
