using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Thẻ thông báo có lựa chọn (GDD v0.3 mục 14.3 + 9.4): chuyện gì đang xảy ra, 2–3 lựa chọn kèm giá ngay,
/// "Không làm gì", và thời gian còn lại để quyết định. Không chặn màn hình (thời gian vẫn trôi).
/// </summary>
public static class EmergencyCard
{
    private static RectTransform _root;
    private static TextMeshProUGUI _title, _body, _timer;
    private static RectTransform _options;
    private static readonly List<GameObject> _buttons = new List<GameObject>();

    public static bool IsOpen => _root != null && UIPopupAnim.IsVisible(_root.gameObject);

    public static void Open()
    {
        EmergencyEvent e = EmergencyOrders.State.openEvent;
        if (e == EmergencyEvent.None || !EnsureUI())
            return;
        UIPopupAnim.Show(_root.gameObject);
        _root.SetAsLastSibling();
        Rebuild(e);
    }

    public static void Close()
    {
        if (_root != null)
            UIPopupAnim.Hide(_root.gameObject);
    }

    private static void Rebuild(EmergencyEvent e)
    {
        foreach (GameObject b in _buttons)
            Object.Destroy(b);
        _buttons.Clear();

        _title.text = EmergencyOrders.GetTitle(e);
        _body.text = EmergencyOrders.GetDescription(e);

        EmergencyOption[] options = EmergencyOrders.GetOptions(e);
        float y = 0f;
        foreach (EmergencyOption o in options)
        {
            EmergencyOrder order = o.order;
            string text = $"<b>{o.label}</b>\n<size=85%><color=#5CCB6A>Được: {o.gain}</color>\n<color=#E86A5A>Giá ngay: {o.cost}</color>"
                + (string.IsNullOrEmpty(o.hint) ? "" : $"\n<i>{o.hint}</i>") + "</size>";
            AddButton(text, y, 96f, () => EmergencyOrders.Choose(order));
            y += 104f;
        }
        if (!EmergencyOrders.IsFollowUp(e))
            AddButton("<b>Không làm gì</b>", y, 44f, () => EmergencyOrders.Choose(EmergencyOrder.None));
    }

    private static void AddButton(string text, float y, float height, UnityEngine.Events.UnityAction onClick)
    {
        Button button = UIWidgets.BoxButton(_options, "Option", "", 15f, onClick, false);
        UIWidgets.TopLeft((RectTransform)button.transform, 0f, y, 400f, height);
        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
        label.text = text;
        label.alignment = TextAlignmentOptions.Left;
        label.textWrappingMode = TextWrappingModes.Normal;
        _buttons.Add(button.gameObject);
    }

    private static bool EnsureUI()
    {
        if (_root != null)
            return true;
        Canvas canvas = RuntimeUIKit.FindHudCanvas();
        if (canvas == null)
            return false;

        _root = UIWidgets.Box(canvas.transform, "EmergencyCard", true);
        _root.anchorMin = _root.anchorMax = _root.pivot = new Vector2(1f, 0.5f);
        _root.anchoredPosition = new Vector2(-24f, 40f);
        _root.sizeDelta = new Vector2(440f, 560f);
        _root.GetComponent<Image>().raycastTarget = true;
        UIPopupAnim.Attach(_root.gameObject, _root);

        _title = UIWidgets.Text(_root, "Txt_Title", "", 22f, UISkin.GoldBright, TextAlignmentOptions.Left, true);
        UIWidgets.TopLeft(_title.rectTransform, 20f, 14f, 400f, 32f);
        _body = UIWidgets.Text(_root, "Txt_Body", "", 15f, UISkin.Cream, TextAlignmentOptions.TopLeft);
        _body.textWrappingMode = TextWrappingModes.Normal;
        UIWidgets.TopLeft(_body.rectTransform, 20f, 50f, 400f, 60f);
        _options = UIWidgets.Rect(_root, "Options");
        UIWidgets.TopLeft(_options, 20f, 116f, 400f, 400f);
        _timer = UIWidgets.Text(_root, "Txt_Timer", "", 14f, UISkin.Muted, TextAlignmentOptions.Left);
        UIWidgets.TopLeft(_timer.rectTransform, 20f, 528f, 400f, 24f);
        _root.gameObject.AddComponent<EmergencyCardTimer>().Bind(_timer);

        _root.gameObject.SetActive(false);
        return true;
    }
}

/// <summary>Cập nhật "thời gian còn lại" của thẻ (tới hết tháng thì tự động Không làm gì).</summary>
public class EmergencyCardTimer : MonoBehaviour
{
    private TextMeshProUGUI _text;

    public void Bind(TextMeshProUGUI text) => _text = text;

    private void Update()
    {
        if (_text == null)
            return;
        GlobalSystemManager g = GamePlayController.Instance != null && GamePlayController.Instance.playerContain != null
            ? GamePlayController.Instance.playerContain.globalSystemManager
            : null;
        if (g == null)
            return;
        float left = Mathf.Clamp01(1f - g.GetGlobalDataRef().timeTickProgress);
        float seconds = g.SpeedMultiplier > 0f ? left * g.RealSecondsPerMonth / g.SpeedMultiplier : float.PositiveInfinity;
        _text.text = float.IsInfinity(seconds)
            ? "Thời gian đang dừng — hết tháng mà chưa chọn sẽ tính là Không làm gì"
            : $"Còn khoảng {Mathf.CeilToInt(seconds)} giây (hết tháng) để quyết định";
    }
}
