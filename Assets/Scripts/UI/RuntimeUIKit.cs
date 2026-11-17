using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Helper dựng UI lúc chạy (popup nhỏ, toast) với style chung của HUD.
/// </summary>
public static class RuntimeUIKit
{
    public static readonly Color PanelColor = new Color32(0xE2, 0xE5, 0xE7, 0xF2);
    public static readonly Color ButtonColor = new Color32(0xD3, 0xD7, 0xDA, 0xFF);
    public static readonly Color ActiveButtonColor = new Color32(0x9F, 0xC8, 0x8A, 0xFF);
    public static readonly Color DangerButtonColor = new Color32(0xE5, 0x9A, 0x9A, 0xFF);
    public static readonly Color TextColor = new Color32(0x33, 0x33, 0x33, 0xFF);

    public static Canvas FindHudCanvas()
    {
        GameObject hud = GameObject.Find("GameplayHUD");
        Canvas found = hud != null ? hud.GetComponentInParent<Canvas>() : null;
        if (found != null)
            return found.rootCanvas;

        foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay)
                return c;
        }

        return null;
    }

    public static bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    /// <summary>Panel dọc tự co theo nội dung, pivot ở đáy giữa (để neo trên đầu đối tượng).</summary>
    public static RectTransform CreatePanel(Transform parent, string name, float width)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(width, 100f);
        go.GetComponent<Image>().color = PanelColor;

        VerticalLayoutGroup layout = go.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 10, 12);
        layout.spacing = 6f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = go.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return rt;
    }

    public static RectTransform CreateRow(Transform parent, string name, float height)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        HorizontalLayoutGroup row = go.GetComponent<HorizontalLayoutGroup>();
        row.spacing = 8f;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = true;
        row.childForceExpandHeight = true;
        go.GetComponent<LayoutElement>().preferredHeight = height;
        return (RectTransform)go.transform;
    }

    public static TextMeshProUGUI CreateText(Transform parent, string name, float size, FontStyles style, TextAlignmentOptions align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.color = TextColor;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.richText = true;
        tmp.raycastTarget = false;
        return tmp;
    }

    public static Image CreateButton(Transform parent, string name, string label, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = ButtonColor;
        go.GetComponent<Button>().onClick.AddListener(onClick);

        TextMeshProUGUI text = CreateText(go.transform, "Label", 16f, FontStyles.Bold, TextAlignmentOptions.Center);
        RectTransform rt = text.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        text.text = label;
        return image;
    }

    /// <summary>Header: tiêu đề + nút X.</summary>
    public static TextMeshProUGUI CreateHeader(Transform panel, UnityEngine.Events.UnityAction onClose)
    {
        RectTransform header = CreateRow(panel, "Header", 26f);
        header.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
        TextMeshProUGUI title = CreateText(header, "Title", 20f, FontStyles.Bold, TextAlignmentOptions.Left);
        title.textWrappingMode = TextWrappingModes.NoWrap;
        title.overflowMode = TextOverflowModes.Ellipsis;
        title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        Image close = CreateButton(header, "Close", "X", onClose);
        LayoutElement closeLayout = close.gameObject.AddComponent<LayoutElement>();
        closeLayout.preferredWidth = 26f;
        closeLayout.minWidth = 26f;
        closeLayout.flexibleWidth = 0f;
        return title;
    }

    /// <summary>
    /// Đặt popup (pivot đáy giữa) tại điểm world, kẹp trong màn hình và chừa thanh HUD trên/dưới.
    /// </summary>
    public static void PlaceAtWorld(RectTransform panel, Canvas canvas, Vector3 world, float topMargin = 130f, float bottomMargin = 140f)
    {
        Camera cam = Camera.main;
        if (cam == null || canvas == null || panel == null)
            return;

        RectTransform canvasRect = (RectTransform)canvas.transform;
        Vector2 screen = cam.WorldToScreenPoint(world);
        Camera uiCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, uiCam, out Vector2 local))
            return;

        Vector2 size = panel.rect.size;
        Rect r = canvasRect.rect;
        local.x = Mathf.Clamp(local.x, r.xMin + size.x * 0.5f, r.xMax - size.x * 0.5f);
        local.y = Mathf.Clamp(local.y, r.yMin + bottomMargin, r.yMax - size.y - topMargin);
        panel.anchoredPosition = local;
    }
}
