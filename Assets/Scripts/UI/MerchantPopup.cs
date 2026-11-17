using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI thương nhân: nút "Thương nhân" trên HUD (chỉ hiện khi thương nhân đang ở Cổng)
/// và cửa sổ mua / bán Lương thực, Than, Gỗ theo lô. UI tự dựng lúc chạy.
/// </summary>
public class MerchantPopup : MonoBehaviour
{
    private const float PanelWidth = 560f;
    private static readonly int[] LotSizes = { 10, 50 };

    private sealed class Row
    {
        public ResourceType type;
        public TextMeshProUGUI info;
    }

    private MerchantManager _merchant;
    private Canvas _canvas;
    private RectTransform _panel;
    private TextMeshProUGUI _title;
    private TextMeshProUGUI _summary;
    private TextMeshProUGUI _status;
    private RectTransform _hudButton;
    private TextMeshProUGUI _hudButtonLabel;
    private Row[] _rows;

    public bool IsOpen => _panel != null && _panel.gameObject.activeSelf;

    public static void EnsureInstance()
    {
        if (FindFirstObjectByType<MerchantPopup>() != null)
            return;
        new GameObject("MerchantPopup").AddComponent<MerchantPopup>();
    }

    private void OnEnable()
    {
        GameEvents.Listen(EventID.MerchantArrived, OnMerchantChanged);
        GameEvents.Listen(EventID.MerchantLeft, OnMerchantChanged);
        GameEvents.Listen(EventID.MerchantTraded, OnMerchantChanged);
        GameEvents.Listen(EventID.ResourcesChanged, OnMerchantChanged);
        GameEvents.Listen(EventID.MonthChanged, OnMerchantChanged);
    }

    private void OnDisable()
    {
        GameEvents.Unlisten(EventID.MerchantArrived, OnMerchantChanged);
        GameEvents.Unlisten(EventID.MerchantLeft, OnMerchantChanged);
        GameEvents.Unlisten(EventID.MerchantTraded, OnMerchantChanged);
        GameEvents.Unlisten(EventID.ResourcesChanged, OnMerchantChanged);
        GameEvents.Unlisten(EventID.MonthChanged, OnMerchantChanged);
    }

    private void Start()
    {
        EnsureUI();
        RefreshAll();
    }

    private void OnMerchantChanged(object param) => RefreshAll();

    public void Toggle()
    {
        if (IsOpen) Hide();
        else Show();
    }

    public void Show()
    {
        MerchantManager merchant = ResolveMerchant();
        if (merchant == null || !merchant.IsPresent)
        {
            NotificationFeed.Show("Hiện không có thương nhân ở Cổng", NoticeLevel.Info, "merchant-none");
            return;
        }

        EnsureUI();
        if (_panel == null)
            return;

        _status.text = string.Empty;
        _panel.gameObject.SetActive(true);
        _panel.SetAsLastSibling();
        RefreshAll();
    }

    public void Hide()
    {
        if (_panel != null)
            _panel.gameObject.SetActive(false);
    }

    // ==========================================
    // CẬP NHẬT
    // ==========================================

    private void RefreshAll()
    {
        MerchantManager merchant = ResolveMerchant();
        bool present = merchant != null && merchant.IsPresent;

        if (_hudButton != null)
        {
            _hudButton.gameObject.SetActive(present);
            if (present)
                _hudButtonLabel.text = $"Thương nhân\n<size=13>còn {merchant.MonthsLeft} tháng</size>";
        }

        if (!IsOpen)
            return;
        if (!present)
        {
            Hide();
            return;
        }

        _title.text = "Thương nhân tại Cổng";
        _summary.text = $"Vàng của bạn: <b>{merchant.GetPlayerGold()}</b>   ·   Vàng thương nhân: <b>{merchant.Budget}</b>   ·   Ở lại: <b>{merchant.MonthsLeft} tháng</b>";

        foreach (Row row in _rows)
        {
            row.info.text =
                $"<b>{MerchantManager.GetResourceName(row.type)}</b>   Kho: {merchant.GetPlayerStock(row.type)}   ·   Thương nhân có: {merchant.GetMerchantStock(row.type)}\n" +
                $"<size=14>Mua <color=#C0392B>{merchant.GetBuyPrice(row.type)}</color> vàng/đv   ·   Bán <color=#1E8449>{merchant.GetSellPrice(row.type)}</color> vàng/đv</size>";
        }
    }

    private void DoBuy(ResourceType type, int amount)
    {
        MerchantManager merchant = ResolveMerchant();
        if (merchant == null)
            return;

        int price = merchant.GetBuyPrice(type);
        int qty = merchant.Buy(type, amount, out string error);
        SetStatus(qty > 0
            ? $"Đã mua {qty} {MerchantManager.GetResourceName(type)} (-{qty * price} vàng)"
            : error, qty > 0);
    }

    private void DoSell(ResourceType type, int amount)
    {
        MerchantManager merchant = ResolveMerchant();
        if (merchant == null)
            return;

        int price = merchant.GetSellPrice(type);
        int qty = merchant.Sell(type, amount, out string error);
        SetStatus(qty > 0
            ? $"Đã bán {qty} {MerchantManager.GetResourceName(type)} (+{qty * price} vàng)"
            : error, qty > 0);
    }

    private void SetStatus(string message, bool success)
    {
        if (_status == null)
            return;
        _status.text = string.IsNullOrEmpty(message) ? string.Empty
            : $"<color={(success ? "#1E8449" : "#C0392B")}>{message}</color>";
    }

    // ==========================================
    // DỰNG UI
    // ==========================================

    private void EnsureUI()
    {
        if (_panel != null)
            return;

        _canvas = RuntimeUIKit.FindHudCanvas();
        if (_canvas == null)
            return;

        BuildHudButton();

        _panel = RuntimeUIKit.CreatePanel(_canvas.transform, "MerchantPanel", PanelWidth);
        _panel.pivot = new Vector2(0.5f, 0.5f);
        _panel.anchoredPosition = Vector2.zero;
        _title = RuntimeUIKit.CreateHeader(_panel, Hide);
        _summary = RuntimeUIKit.CreateText(_panel, "Summary", 15f, FontStyles.Normal, TextAlignmentOptions.Left);

        _rows = new Row[MerchantManager.TradeGoods.Length];
        for (int i = 0; i < _rows.Length; i++)
            _rows[i] = BuildRow(MerchantManager.TradeGoods[i]);

        _status = RuntimeUIKit.CreateText(_panel, "Status", 15f, FontStyles.Italic, TextAlignmentOptions.Center);
        _status.gameObject.AddComponent<LayoutElement>().preferredHeight = 22f;

        _panel.gameObject.SetActive(false);
    }

    private Row BuildRow(ResourceType type)
    {
        var row = new Row { type = type };

        GameObject box = new GameObject("Row_" + type, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        box.transform.SetParent(_panel, false);
        box.GetComponent<Image>().color = new Color32(0xD9, 0xDD, 0xE0, 0xFF);
        VerticalLayoutGroup v = box.GetComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(10, 10, 6, 8);
        v.spacing = 4f;
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;

        row.info = RuntimeUIKit.CreateText(box.transform, "Info", 16f, FontStyles.Normal, TextAlignmentOptions.Left);

        RectTransform buttons = RuntimeUIKit.CreateRow(box.transform, "Buttons", 32f);
        foreach (int lot in LotSizes)
        {
            int amount = lot;
            RuntimeUIKit.CreateButton(buttons, "Buy" + lot, $"Mua {lot}", () => DoBuy(type, amount));
        }
        foreach (int lot in LotSizes)
        {
            int amount = lot;
            RuntimeUIKit.CreateButton(buttons, "Sell" + lot, $"Bán {lot}", () => DoSell(type, amount));
        }

        return row;
    }

    private void BuildHudButton()
    {
        Image button = RuntimeUIKit.CreateButton(_canvas.transform, "Btn_Merchant", "Thương nhân", Toggle);
        button.color = new Color32(0xF2, 0xC9, 0x4C, 0xFF);
        _hudButton = button.rectTransform;
        // Góc dưới trái, ngang thanh xây dựng
        _hudButton.anchorMin = _hudButton.anchorMax = new Vector2(0f, 0f);
        _hudButton.pivot = new Vector2(0f, 0f);
        _hudButton.anchoredPosition = new Vector2(34f, 34f);
        _hudButton.sizeDelta = new Vector2(150f, 64f);
        _hudButtonLabel = button.GetComponentInChildren<TextMeshProUGUI>();
        _hudButton.gameObject.SetActive(false);
    }

    private MerchantManager ResolveMerchant()
    {
        if (_merchant == null)
        {
            PlayerContain contain = FindFirstObjectByType<PlayerContain>();
            _merchant = contain != null ? contain.merchantManager : FindFirstObjectByType<MerchantManager>();
        }
        return _merchant;
    }
}
