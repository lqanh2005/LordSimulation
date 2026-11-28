using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cửa sổ Ngân khố (GDD v0.3 mục 6.4): ba đòn bẩy — mức lương, thuế thu nhập (cần Tòa thị chính = Tax Office),
/// chính sách hộ thiếu tiền (cấp phát / cắt khẩu phần) — và dòng tiền tháng gần nhất. Mở bằng cách bấm vào số Vàng trên HUD.
/// Đổi mức lương / thuế có hiệu lực từ tháng sau.
/// </summary>
public static class TreasuryWindow
{
    private static UIWidgets.Window _window;
    private static TextMeshProUGUI _wageLabel, _taxLabel, _policyLabel, _report;

    public static bool IsOpen => _window != null && _window.overlay != null && UIPopupAnim.IsVisible(_window.overlay.gameObject);

    public static void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    public static void Open()
    {
        if (!EnsureUI())
            return;
        UIPopupAnim.Show(_window.overlay.gameObject);
        _window.overlay.SetAsLastSibling();
        Refresh();
    }

    public static void Close()
    {
        if (_window != null && _window.overlay != null)
            UIPopupAnim.Hide(_window.overlay.gameObject);
    }

    public static void RefreshIfOpen()
    {
        if (IsOpen)
            Refresh();
    }

    private static GlobalSystemManager Global =>
        GamePlayController.Instance != null && GamePlayController.Instance.playerContain != null
            ? GamePlayController.Instance.playerContain.globalSystemManager
            : null;

    private static void CycleWage()
    {
        GlobalSystemManager g = Global;
        if (g == null) return;
        g.SetPendingWage((WageLevel)(((int)g.GetGlobalDataRef().pendingWageLevel + 1) % 5));
        Refresh();
    }

    private static void CycleIncomeTax()
    {
        GlobalSystemManager g = Global;
        if (g == null) return;
        g.SetPendingIncomeTax((EntryTaxLevel)(((int)g.GetGlobalDataRef().pendingIncomeTaxLevel + 1) % 5));
        Refresh();
    }

    private static void TogglePolicy()
    {
        GlobalSystemManager g = Global;
        if (g == null) return;
        g.SetFeedPolicy(g.GetGlobalDataRef().feedPolicy == FeedPolicy.Subsidize ? FeedPolicy.CutRations : FeedPolicy.Subsidize);
        Refresh();
    }

    private static void Refresh()
    {
        GlobalSystemManager g = Global;
        if (g == null || _wageLabel == null)
            return;
        ref GlobalSystemData d = ref g.GetGlobalDataRef();

        string wage = $"Mức lương: <b>{EconomyRules.WageLabel(d.wageLevel)}</b> (×{EconomyRules.GetWageMultiplier(d.wageLevel):0.##})";
        if (d.pendingWageLevel != d.wageLevel)
            wage += $" → <color=#FFD77E>{EconomyRules.WageLabel(d.pendingWageLevel)} từ tháng sau</color>";
        _wageLabel.text = wage;

        bool taxOffice = g.CurrentBuildingEffects.hasTaxOffice;
        string tax = $"Thuế thu nhập: <b>{StabilityRules.EntryTaxLabel(d.incomeTaxLevel)}</b> ({StabilityRules.GetEntryTaxPercent(d.incomeTaxLevel):0}% lương)";
        if (d.pendingIncomeTaxLevel != d.incomeTaxLevel)
            tax += $" → <color=#FFD77E>{StabilityRules.EntryTaxLabel(d.pendingIncomeTaxLevel)} từ tháng sau</color>";
        if (!taxOffice)
            tax += "\n<size=85%><color=#E86A5A>Chưa thu được — cần xây Tòa thị chính (Tax Office)</color></size>";
        _taxLabel.text = tax;

        _policyLabel.text = d.feedPolicy == FeedPolicy.Subsidize
            ? "Hộ thiếu tiền ăn: <b>Cấp phát</b> <size=85%>(vẫn cho ăn, thành chịu lỗ)</size>"
            : "Hộ thiếu tiền ăn: <b><color=#E86A5A>Cắt khẩu phần</color></b> <size=85%>(hộ đó bị đói)</size>";

        EconomyReport r = EconomySystem.LastReport;
        var sb = new System.Text.StringBuilder(768);
        // GDD 6.6: giá thả nổi theo thị trường + sự kiện; người chơi không tự đặt giá
        sb.Append("<b>Giá thị trường</b> (vàng/đơn vị)\n");
        sb.Append(MarketSystem.DescribePrice(ResourceType.Food, in d)).Append("   ")
          .Append(MarketSystem.DescribePrice(ResourceType.Coal, in d)).Append("   ")
          .Append(MarketSystem.DescribePrice(ResourceType.Wood, in d)).Append('\n');
        if (d.marketActiveEvent != MarketEventType.None)
            sb.Append("<color=#FFD77E>").Append(MarketSystem.EventLabel(d.marketActiveEvent)).Append(": còn ").Append(d.marketActiveMonths).Append(" tháng</color>\n");
        if (d.marketPendingEvent != MarketEventType.None && d.marketPendingInMonths <= 1)
            sb.Append("<color=#FFD77E>Sắp tới: ").Append(MarketSystem.EventLabel(d.marketPendingEvent)).Append("</color>\n");
        if (d.blizzardForecast)
            sb.Append("<color=#E86A5A>Dự báo bão tuyết tháng sau: than, gỗ ×2</color>\n");
        sb.Append("Tiền ăn ").Append(EconomySystem.LastFoodFee.ToString("0.#")).Append("/người · sưởi ")
          .Append(EconomySystem.LastHeatFee.ToString("0.#")).Append("/người (mùa đông)\n\n");
        sb.Append("<b>Tháng trước</b>\n");
        sb.Append("Trả lương: <color=#E86A5A>−").Append(r.wagesPaid).Append("</color>");
        if (r.wagesPaid < r.wagesDue) sb.Append(" / cần ").Append(r.wagesDue);
        sb.Append('\n');
        sb.Append("Thuế thu nhập: <color=#5CCB6A>+").Append(r.incomeTax).Append("</color>\n");
        sb.Append("Hộ trả tiền ăn + sưởi: <color=#5CCB6A>+").Append(r.householdFees).Append("</color>\n");
        if (r.subsidy > 0) sb.Append("Cấp phát cho hộ thiếu tiền: <color=#E86A5A>−").Append(r.subsidy).Append("</color>\n");
        sb.Append("Kho vàng thay đổi: ").Append(r.netGold >= 0 ? "<color=#5CCB6A>+" : "<color=#E86A5A>").Append(r.netGold).Append("</color>\n");
        if (r.totalDebt > 0) sb.Append("<color=#E86A5A>Nợ lương còn lại: ").Append(r.totalDebt).Append(" vàng</color>\n");
        if (r.strikers > 0) sb.Append("<color=#E86A5A>Đang đình công: ").Append(Mathf.RoundToInt(r.strikeRate * 100f)).Append("% lao động</color>\n");
        if (r.cutHouseholds > 0) sb.Append("<color=#E86A5A>Hộ bị cắt khẩu phần: ").Append(r.cutHouseholds).Append("</color>\n");
        sb.Append("\n<size=85%><i>Lương ròng dân để dành thành Wealth. Lương cao: dân vui, làm tốt hơn nhưng thành tốn hơn. ")
          .Append("Thuế cao: thu nhiều vàng nhưng dân bất mãn, hộ dễ thiếu tiền.</i></size>");
        _report.text = sb.ToString();
    }

    private static bool EnsureUI()
    {
        if (_window != null && _window.overlay != null)
            return true;

        Canvas canvas = RuntimeUIKit.FindHudCanvas();
        if (canvas == null)
            return false;

        _window = UIWidgets.CreateWindow(canvas.transform, "TreasuryWindow", new Vector2(620f, 680f), "Ngân khố", Close, 48f);

        _wageLabel = Lever(_window.frame, "Btn_Wage", 100f, CycleWage);
        _taxLabel = Lever(_window.frame, "Btn_IncomeTax", 170f, CycleIncomeTax);
        _policyLabel = Lever(_window.frame, "Btn_Policy", 240f, TogglePolicy);

        _report = UIWidgets.Text(_window.frame, "Txt_Report", "", 16f, UISkin.Cream, TextAlignmentOptions.TopLeft);
        _report.textWrappingMode = TextWrappingModes.Normal;
        _report.overflowMode = TextOverflowModes.Overflow;
        UIWidgets.TopLeft(_report.rectTransform, 50f, 310f, 520f, 330f);

        _window.overlay.gameObject.SetActive(false);
        return true;
    }

    private static TextMeshProUGUI Lever(RectTransform parent, string name, float y, UnityEngine.Events.UnityAction onClick)
    {
        Button button = UIWidgets.BoxButton(parent, name, "", 16f, onClick, false);
        UIWidgets.TopLeft((RectTransform)button.transform, 50f, y, 520f, 58f);
        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Left;
        label.textWrappingMode = TextWrappingModes.Normal;
        return label;
    }
}
