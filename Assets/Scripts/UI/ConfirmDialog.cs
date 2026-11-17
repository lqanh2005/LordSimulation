using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hộp thoại nhỏ 586x303 theo mockup: Alert (một nút Đóng), Xác nhận lưu / tải / khôi phục (Đóng + hành động).
/// Dựng lúc chạy trên canvas đang mở cao nhất, luôn nằm trên cùng.
/// </summary>
public static class ConfirmDialog
{
    public enum Icon { Alert, Save, Load }

    private static RectTransform _overlay;
    private static Image _icon;
    private static TextMeshProUGUI _message;
    private static Button _confirm;
    private static Button _close;
    private static TextMeshProUGUI _confirmLabel;
    private static Action _onConfirm;

    public static bool IsOpen => _overlay != null && UIPopupAnim.IsVisible(_overlay.gameObject);

    /// <summary>Cảnh báo một nút: icon tam giác đỏ + nội dung + "Đóng".</summary>
    public static void Alert(string message, Action onClose = null)
    {
        Show(Icon.Alert, message, null, onClose);
    }

    /// <summary>Hỏi xác nhận: "Đóng" huỷ, nút còn lại chạy onConfirm.</summary>
    public static void Ask(Icon icon, string message, string confirmLabel, Action onConfirm)
    {
        Show(icon, message, confirmLabel, onConfirm);
    }

    public static void Close()
    {
        if (_overlay != null)
            UIPopupAnim.Hide(_overlay.gameObject);
        _onConfirm = null;
    }

    private static void Show(Icon icon, string message, string confirmLabel, Action action)
    {
        if (!EnsureUI())
            return;

        UISkin skin = UISkin.Instance;
        Sprite sprite = skin == null ? null : icon switch
        {
            Icon.Save => skin.saveIcon,
            Icon.Load => skin.loadIcon,
            _ => skin.alertIcon
        };
        _icon.sprite = sprite;
        _icon.enabled = sprite != null;
        _message.text = message;

        bool twoButtons = !string.IsNullOrEmpty(confirmLabel);
        _confirm.gameObject.SetActive(twoButtons);
        _confirmLabel.text = confirmLabel ?? string.Empty;
        _onConfirm = action;

        RectTransform close = (RectTransform)_close.transform;
        close.anchoredPosition = new Vector2(twoButtons ? -101f : 0f, close.anchoredPosition.y);

        UIPopupAnim.Show(_overlay.gameObject);
        _overlay.SetAsLastSibling();
    }

    private static bool EnsureUI()
    {
        if (_overlay != null)
            return true;

        Canvas canvas = RuntimeUIKit.FindHudCanvas();
        if (canvas == null)
            canvas = FindTopCanvas();
        if (canvas == null)
            return false;

        _overlay = UIWidgets.Rect(canvas.transform, "ConfirmDialog");
        UIWidgets.Stretch(_overlay);
        UIWidgets.Image(_overlay, null, UISkin.Overlay, true);

        UISkin skin = UISkin.Instance;
        RectTransform frame = UIWidgets.Rect(_overlay, "Window");
        UIWidgets.Center(frame, 586f, 303f);
        UIWidgets.Image(frame, skin != null ? skin.dialogFrame : null, skin != null ? Color.white : RuntimeUIKit.PanelColor, true);

        RectTransform iconRt = UIWidgets.Rect(frame, "Icon");
        iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 1f);
        iconRt.pivot = new Vector2(0.5f, 0.5f);
        iconRt.anchoredPosition = new Vector2(0f, -68f);
        iconRt.sizeDelta = new Vector2(72f, 72f);
        _icon = iconRt.gameObject.AddComponent<Image>();
        _icon.preserveAspect = true;
        _icon.raycastTarget = false;

        _message = UIWidgets.Text(frame, "Txt_Message", "", 21f, UISkin.Cream);
        _message.textWrappingMode = TextWrappingModes.Normal;
        RectTransform mr = _message.rectTransform;
        mr.anchorMin = mr.anchorMax = new Vector2(0.5f, 1f);
        mr.pivot = new Vector2(0.5f, 0.5f);
        mr.anchoredPosition = new Vector2(0f, -166f);
        mr.sizeDelta = new Vector2(520f, 60f);

        _close = UIWidgets.BoxButton(frame, "Btn_Close", "Đóng", 18f, OnClose);
        PlaceBottom((RectTransform)_close.transform, 0f);

        _confirm = UIWidgets.BoxButton(frame, "Btn_Confirm", "", 18f, OnConfirm);
        PlaceBottom((RectTransform)_confirm.transform, 101f);
        _confirmLabel = _confirm.GetComponentInChildren<TextMeshProUGUI>();

        UIPopupAnim.Attach(_overlay.gameObject, frame);
        _overlay.gameObject.SetActive(false);
        return true;
    }

    private static void PlaceBottom(RectTransform rt, float x)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(x, 26f);
        rt.sizeDelta = new Vector2(170f, 52f);
    }

    private static void OnClose()
    {
        // Alert: callback chạy khi đóng; hỏi xác nhận: Đóng = huỷ
        Action action = _onConfirm;
        bool isAlert = !_confirm.gameObject.activeSelf;
        Close();
        if (isAlert)
            action?.Invoke();
    }

    private static void OnConfirm()
    {
        Action action = _onConfirm;
        Close();
        action?.Invoke();
    }

    private static Canvas FindTopCanvas()
    {
        Canvas best = null;
        foreach (Canvas c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (!c.isRootCanvas || c.renderMode != RenderMode.ScreenSpaceOverlay || !c.isActiveAndEnabled)
                continue;
            if (best == null || c.sortingOrder > best.sortingOrder)
                best = c;
        }
        return best;
    }
}
