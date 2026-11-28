using System.IO;
using UnityEngine;

/// <summary>Trạng thái thương nhân lưu vào save (v8+).</summary>
public struct MerchantSaveData
{
    public bool isPresent;
    public byte monthsLeft;          // Số tháng còn ở lại (gồm tháng hiện tại)
    public byte monthsSinceVisit;    // Đếm tháng từ lần ghé trước (bảo đảm không vắng quá lâu)
    public int budget;               // Vàng thương nhân còn để mua hàng của người chơi
    public int foodStock, coalStock, woodStock;
    public int foodPrice, coalPrice, woodPrice;   // Giá người chơi MUA 1 đơn vị

    public void Write(BinaryWriter w)
    {
        w.Write(isPresent);
        w.Write(monthsLeft);
        w.Write(monthsSinceVisit);
        w.Write(budget);
        w.Write(foodStock); w.Write(coalStock); w.Write(woodStock);
        w.Write(foodPrice); w.Write(coalPrice); w.Write(woodPrice);
    }

    public static MerchantSaveData Read(BinaryReader r)
    {
        return new MerchantSaveData
        {
            isPresent = r.ReadBoolean(),
            monthsLeft = r.ReadByte(),
            monthsSinceVisit = r.ReadByte(),
            budget = r.ReadInt32(),
            foodStock = r.ReadInt32(), coalStock = r.ReadInt32(), woodStock = r.ReadInt32(),
            foodPrice = r.ReadInt32(), coalPrice = r.ReadInt32(), woodPrice = r.ReadInt32()
        };
    }
}

/// <summary>
/// GDD MVP — Trade: thỉnh thoảng một thương nhân đến Cổng, người chơi mua / bán Lương thực, Than, Gỗ bằng Vàng.
/// - Mỗi tháng (khi vắng) có xác suất ghé, tăng theo Uy tín, giảm vào mùa đông; vắng quá lâu thì chắc chắn ghé.
/// - Ở lại vài tháng. Giá theo thị trường (MarketSystem): bán ×1,25, mua ×0,75 giá hiện tại.
/// - Thương nhân có giới hạn hàng để bán và vàng để mua lại hàng của người chơi.
/// </summary>
public class MerchantManager : MonoBehaviour
{
    public static readonly ResourceType[] TradeGoods = { ResourceType.Food, ResourceType.Coal, ResourceType.Wood };

    [Header("Tần suất ghé")]
    [SerializeField, Range(0f, 1f)] private float baseVisitChance = 0.2f;
    [Tooltip("Uy tín 100 cộng thêm bao nhiêu xác suất (uy tín âm thì trừ)")]
    [SerializeField, Range(0f, 1f)] private float reputationVisitBonus = 0.2f;
    [SerializeField, Range(0f, 1f)] private float winterVisitMultiplier = 0.5f;
    [Tooltip("Vắng quá số tháng này thì chắc chắn ghé")]
    [SerializeField, Min(1)] private int maxMonthsAbsent = 4;
    [SerializeField, Min(1)] private int stayMonths = 2;

    [Header("Hàng & giá")]
    [SerializeField] private Vector2Int stockRange = new Vector2Int(30, 80);
    [SerializeField] private Vector2Int budgetRange = new Vector2Int(60, 150);

    [SerializeField] private GlobalSystemManager globalSystemManager;

    private MerchantSaveData _state;

    public bool IsPresent => _state.isPresent;
    public int MonthsLeft => _state.monthsLeft;
    public int Budget => _state.budget;

    public void Init()
    {
        if (globalSystemManager == null)
            globalSystemManager = HUDLocator.ResolveGlobalSystem(null);
        _state = default;
    }

    private void OnEnable()
    {
        GameEvents.Listen(EventID.MonthChanged, OnMonthChanged);
    }

    private void OnDisable()
    {
        GameEvents.Unlisten(EventID.MonthChanged, OnMonthChanged);
    }

    // ==========================================
    // LƯU / TẢI
    // ==========================================

    public MerchantSaveData GetSaveData() => _state;

    public void ApplySaveData(in MerchantSaveData data)
    {
        _state = data;
        GameEvents.Post(_state.isPresent ? EventID.MerchantArrived : EventID.MerchantLeft, false);
    }

    // ==========================================
    // THEO THÁNG
    // ==========================================

    private void OnMonthChanged(object param)
    {
        if (!(param is MonthChangedPayload p) || !p.isSimulationTick)
            return;

        if (_state.isPresent)
        {
            if (_state.monthsLeft > 0)
                _state.monthsLeft--;
            if (_state.monthsLeft == 0)
            {
                _state.isPresent = false;
                _state.monthsSinceVisit = 0;
                GameEvents.Post(EventID.MerchantLeft, true);
            }
            return;
        }

        if (_state.monthsSinceVisit < byte.MaxValue)
            _state.monthsSinceVisit++;

        if (ShouldVisit(p.season))
            Arrive(p.season);
    }

    private bool ShouldVisit(SeasonType season)
    {
        if (EmergencyOrders.GateClosed)
            return false;   // GDD 11: Đóng cổng thì thương nhân cũng không vào được

        if (_state.monthsSinceVisit >= maxMonthsAbsent)
            return true;

        float reputation = globalSystemManager != null ? globalSystemManager.GetGlobalDataRef().reputation : 0f;
        // Reputation 0–100 (mốc 50): uy tín trên 50 thương nhân ghé nhiều hơn, dưới 50 ít hơn
        float chance = baseVisitChance + reputationVisitBonus
            * (Mathf.Clamp(reputation, StabilityRules.ReputationMin, StabilityRules.ReputationMax) - StabilityRules.ReputationStart) / 50f;
        if (season == SeasonType.Winter)
            chance *= winterVisitMultiplier;

        return Random.value < Mathf.Clamp01(chance);
    }

    private void Arrive(SeasonType season)
    {
        _state.isPresent = true;
        _state.monthsLeft = (byte)Mathf.Clamp(stayMonths, 1, 12);
        _state.monthsSinceVisit = 0;
        _state.budget = Random.Range(budgetRange.x, budgetRange.y + 1);
        _state.foodStock = RollStock();
        _state.coalStock = RollStock();
        _state.woodStock = RollStock();
        // Giá không chốt lúc tới nữa: luôn theo giá thị trường hiện tại (MarketSystem)

        GameEvents.Post(EventID.MerchantArrived, true);
    }

    private int RollStock() => Random.Range(stockRange.x, stockRange.y + 1);


    // ==========================================
    // MUA / BÁN
    // ==========================================

    // GDD v0.3 mục 6.6: thương nhân bán cho thành giá ×1,25 và mua của thành giá ×0,75 giá thị trường hiện tại
    public const float MerchantSellMarkup = 1.25f;
    public const float MerchantBuyMarkdown = 0.75f;

    private float MarketPrice(ResourceType type) =>
        globalSystemManager != null ? MarketSystem.GetPrice(type, in globalSystemManager.GetGlobalDataRef()) : MarketSystem.GetBasePrice(type);

    /// <summary>Giá người chơi MUA 1 đơn vị của thương nhân.</summary>
    public int GetBuyPrice(ResourceType type) =>
        System.Array.IndexOf(TradeGoods, type) < 0 ? 0 : Mathf.Max(1, Mathf.CeilToInt(MarketPrice(type) * MerchantSellMarkup - 0.001f));

    /// <summary>Giá thương nhân trả khi người chơi bán 1 đơn vị.</summary>
    public int GetSellPrice(ResourceType type) =>
        System.Array.IndexOf(TradeGoods, type) < 0 ? 0 : Mathf.Max(1, Mathf.FloorToInt(MarketPrice(type) * MerchantBuyMarkdown + 0.001f));

    public int GetMerchantStock(ResourceType type) => type switch
    {
        ResourceType.Food => _state.foodStock,
        ResourceType.Coal => _state.coalStock,
        ResourceType.Wood => _state.woodStock,
        _ => 0
    };

    public int GetPlayerStock(ResourceType type)
    {
        if (globalSystemManager == null)
            return 0;
        ref GlobalSystemData d = ref globalSystemManager.GetGlobalDataRef();
        return type switch
        {
            ResourceType.Food => d.stockFood,
            ResourceType.Coal => d.stockCoal,
            ResourceType.Wood => d.stockWood,
            _ => 0
        };
    }

    public int GetPlayerGold() => globalSystemManager != null ? globalSystemManager.GetGlobalDataRef().treasuryGold : 0;

    /// <summary>Mua tối đa <paramref name="amount"/> đơn vị (giảm theo hàng / vàng còn lại). Trả về số mua được.</summary>
    public int Buy(ResourceType type, int amount, out string error)
    {
        error = null;
        if (!_state.isPresent || globalSystemManager == null) { error = "Thương nhân không có ở Cổng"; return 0; }

        int price = GetBuyPrice(type);
        int canAfford = price > 0 ? GetPlayerGold() / price : 0;
        int qty = Mathf.Min(amount, GetMerchantStock(type), canAfford);
        if (qty <= 0)
        {
            error = GetMerchantStock(type) <= 0 ? "Thương nhân đã hết hàng này" : "Không đủ vàng";
            return 0;
        }

        globalSystemManager.ModifyGold(-qty * price);
        globalSystemManager.AddResource(type, qty);
        AddMerchantStock(type, -qty);
        _state.budget += qty * price;
        GameEvents.Post(EventID.MerchantTraded);
        return qty;
    }

    /// <summary>Bán tối đa <paramref name="amount"/> đơn vị (giảm theo kho / vàng của thương nhân). Trả về số bán được.</summary>
    public int Sell(ResourceType type, int amount, out string error)
    {
        error = null;
        if (!_state.isPresent || globalSystemManager == null) { error = "Thương nhân không có ở Cổng"; return 0; }

        int price = GetSellPrice(type);
        int merchantCanPay = price > 0 ? _state.budget / price : 0;
        int qty = Mathf.Min(amount, GetPlayerStock(type), merchantCanPay);
        if (qty <= 0)
        {
            error = GetPlayerStock(type) <= 0 ? "Kho không còn hàng này" : "Thương nhân hết vàng";
            return 0;
        }

        globalSystemManager.ConsumeAvailable(type, qty);
        globalSystemManager.ModifyGold(qty * price);
        AddMerchantStock(type, qty);
        _state.budget -= qty * price;
        GameEvents.Post(EventID.MerchantTraded);
        return qty;
    }

    private void AddMerchantStock(ResourceType type, int delta)
    {
        switch (type)
        {
            case ResourceType.Food: _state.foodStock = Mathf.Max(0, _state.foodStock + delta); break;
            case ResourceType.Coal: _state.coalStock = Mathf.Max(0, _state.coalStock + delta); break;
            case ResourceType.Wood: _state.woodStock = Mathf.Max(0, _state.woodStock + delta); break;
        }
    }

    public static string GetResourceName(ResourceType type) => type switch
    {
        ResourceType.Food => "Lương thực",
        ResourceType.Coal => "Than",
        ResourceType.Wood => "Gỗ",
        ResourceType.Iron => "Sắt",
        ResourceType.Medicine => "Thuốc men",
        _ => type.ToString()
    };
}
