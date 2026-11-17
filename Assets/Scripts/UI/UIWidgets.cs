using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Bộ widget dựng lúc chạy theo mockup Assets/UI (cửa sổ khung vát góc, tab, slider, checkbox,
/// dropdown, nút hộp viền vàng...). Toạ độ theo kiểu mockup: (x, y) là góc trái trên tính từ góc
/// trái trên của cha, đơn vị pixel ở độ phân giải tham chiếu 1920x1080.
/// </summary>
public static class UIWidgets
{
    private static UISkin Skin => UISkin.Instance;

    // ==========================================
    // BỐ CỤC
    // ==========================================

    public static RectTransform Rect(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static void TopLeft(RectTransform rt, float x, float y, float width, float height)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(width, height);
    }

    public static void Center(RectTransform rt, float width, float height)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(width, height);
    }

    public static void Stretch(RectTransform rt, float inset = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    public static Image Image(RectTransform rt, Sprite sprite, Color color, bool raycast = false)
    {
        Image image = rt.gameObject.GetComponent<Image>();
        if (image == null)
            image = rt.gameObject.AddComponent<Image>();
        UISkin.SetSliced(image, sprite, color);
        image.raycastTarget = raycast;
        return image;
    }

    public static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color,
        TextAlignmentOptions align = TextAlignmentOptions.Center, bool title = false)
    {
        RectTransform rt = Rect(parent, name);
        TextMeshProUGUI tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.richText = true;
        UISkin.StyleText(tmp, color, title);
        return tmp;
    }

    // ==========================================
    // CỬA SỔ
    // ==========================================

    public class Window
    {
        public RectTransform overlay;   // phủ mờ toàn màn hình, bật/tắt cái này để hiện/ẩn
        public RectTransform frame;     // khung chính (toạ độ con tính từ góc trái trên khung)
        public TextMeshProUGUI title;
    }

    /// <summary>
    /// Cửa sổ chuẩn mockup: lớp phủ tối + khung BackPopup + tiêu đề vàng có gạch hai bên + nút X.
    /// </summary>
    public static Window CreateWindow(Transform parent, string name, Vector2 size, string title, UnityAction onClose,
        float titleY = 56f, float ornamentGap = -1f, float ornamentLine = 155f)
    {
        var window = new Window();

        window.overlay = Rect(parent, name);
        Stretch(window.overlay);
        Image(window.overlay, null, UISkin.Overlay, true);

        window.frame = Rect(window.overlay, "Window");
        Center(window.frame, size.x, size.y);
        Image(window.frame, Skin != null ? Skin.popupFrame : null, Skin != null ? Color.white : RuntimeUIKit.PanelColor, true);

        if (!string.IsNullOrEmpty(title))
        {
            window.title = Text(window.frame, "Txt_Title", title, 30f, UISkin.GoldBright, TextAlignmentOptions.Center, true);
            RectTransform tr = window.title.rectTransform;
            tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 1f);
            tr.pivot = new Vector2(0.5f, 0.5f);
            tr.anchoredPosition = new Vector2(0f, -titleY);
            tr.sizeDelta = new Vector2(400f, 44f);

            float gap = ornamentGap >= 0f ? ornamentGap : Mathf.Max(80f, window.title.GetPreferredValues(title).x + 40f);
            Ornament(window.frame, titleY, gap, ornamentLine);
        }

        if (onClose != null)
        {
            RectTransform close = Rect(window.frame, "Btn_Close");
            close.anchorMin = close.anchorMax = Vector2.one;
            close.pivot = new Vector2(0.5f, 0.5f);
            close.anchoredPosition = new Vector2(-46f, -titleY);
            close.sizeDelta = new Vector2(30f, 30f);
            Image img = Image(close, null, Color.white, true);
            UISkin.SetIcon(img, Skin != null ? Skin.closeIcon : null);
            if (img.sprite == null)
                Text(close, "X", "X", 24f, UISkin.Gold);
            Button button = close.gameObject.AddComponent<Button>();
            UISkin.SetButtonTint(button);
            button.onClick.AddListener(RuntimeUIKit.PlayClick);
            button.onClick.AddListener(onClose);
        }

        UIPopupAnim.Attach(window.overlay.gameObject, window.frame);
        return window;
    }

    /// <summary>Gạch "———◇ [gap] ◇———" căn giữa ngang tại y. gap = 0: một hình thoi ở giữa.</summary>
    public static void Ornament(RectTransform parent, float centerY, float gap, float lineLength)
    {
        if (Skin == null)
            return;

        RectTransform root = Rect(parent, "Ornament");
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 1f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.anchoredPosition = new Vector2(0f, -centerY);
        root.sizeDelta = new Vector2(gap + 2f * (lineLength + 14f), 12f);

        bool single = gap <= 0f;
        float half = gap * 0.5f;
        Decor(root, Skin.titleLine, new Vector2(-half - (single ? 8f : 14f), 0f), new Vector2(1f, 0.5f), new Vector2(lineLength, 3f));
        Decor(root, Skin.titleLineRight, new Vector2(half + (single ? 8f : 14f), 0f), new Vector2(0f, 0.5f), new Vector2(lineLength, 3f));
        Decor(root, Skin.rhombus, new Vector2(single ? 0f : -half - 6f, 0f), new Vector2(0.5f, 0.5f), new Vector2(9f, 9f));
        if (!single)
            Decor(root, Skin.rhombus, new Vector2(half + 6f, 0f), new Vector2(0.5f, 0.5f), new Vector2(9f, 9f));
    }

    private static void Decor(RectTransform parent, Sprite sprite, Vector2 pos, Vector2 pivot, Vector2 size)
    {
        RectTransform rt = Rect(parent, "Decor");
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
    }

    /// <summary>Hộp tối viền vàng mảnh (bright = viền vàng sáng — đang chọn).</summary>
    public static RectTransform Box(Transform parent, string name, bool bright = false)
    {
        RectTransform rt = Rect(parent, name);
        Image(rt, Skin != null ? (bright ? Skin.boxGoldBright : Skin.boxGold) : null, Skin != null ? Color.white : RuntimeUIKit.ButtonColor);
        return rt;
    }

    public static void SetBoxBright(Image image, bool bright)
    {
        if (Skin != null)
            UISkin.SetSliced(image, bright ? Skin.boxGoldBright : Skin.boxGold, Color.white);
    }

    // ==========================================
    // NÚT
    // ==========================================

    /// <summary>Nút hộp viền vàng có chữ (Kích hoạt, Lưu game, Đóng...).</summary>
    public static Button BoxButton(Transform parent, string name, string label, float fontSize, UnityAction onClick, bool title = true)
    {
        RectTransform rt = Box(parent, name);
        rt.GetComponent<Image>().raycastTarget = true;
        Button button = rt.gameObject.AddComponent<Button>();
        UISkin.SetButtonTint(button);
        button.onClick.AddListener(RuntimeUIKit.PlayClick);
        if (onClick != null)
            button.onClick.AddListener(onClick);

        TextMeshProUGUI text = Text(rt, "Label", label, fontSize, UISkin.Cream, TextAlignmentOptions.Center, title);
        Stretch(text.rectTransform, 6f);
        return button;
    }

    /// <summary>Tab kiểu mockup Cài đặt (Tab.png / Tab_Select.png).</summary>
    public static Button TabButton(Transform parent, string name, string label, UnityAction onClick)
    {
        RectTransform rt = Rect(parent, name);
        Image img = Image(rt, Skin != null ? Skin.tab : null, Skin != null ? Color.white : RuntimeUIKit.ButtonColor, true);
        Button button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        UISkin.SetButtonTint(button);
        button.onClick.AddListener(RuntimeUIKit.PlayClick);
        button.onClick.AddListener(onClick);
        TextMeshProUGUI text = Text(rt, "Label", label, 19f, UISkin.Cream, TextAlignmentOptions.Center, true);
        Stretch(text.rectTransform, 8f);
        return button;
    }

    public static void SetTabSelected(Button tab, bool selected)
    {
        if (tab == null || Skin == null)
            return;
        UISkin.SetSliced((Image)tab.targetGraphic, selected ? Skin.tabSelected : Skin.tab, Color.white);
    }

    // ==========================================
    // ĐIỀU KHIỂN
    // ==========================================

    /// <summary>Ô tick (On.png / off.png). Trả về Toggle; onChange nhận giá trị mới.</summary>
    public static Toggle Checkbox(Transform parent, string name, bool value, UnityAction<bool> onChange)
    {
        RectTransform rt = Rect(parent, name);
        Image background = Image(rt, Skin != null ? Skin.checkboxOff : null, Skin != null ? Color.white : RuntimeUIKit.ButtonColor, true);
        background.type = UnityEngine.UI.Image.Type.Simple;

        RectTransform checkRt = Rect(rt, "Checkmark");
        Stretch(checkRt);
        Image check = checkRt.gameObject.AddComponent<Image>();
        check.sprite = Skin != null ? Skin.checkboxOn : null;
        check.color = Skin != null ? Color.white : UISkin.GoldBright;
        check.raycastTarget = false;

        Toggle toggle = rt.gameObject.AddComponent<Toggle>();
        toggle.targetGraphic = background;
        toggle.graphic = check;
        toggle.toggleTransition = Toggle.ToggleTransition.None;
        UISkin.SetButtonTint(toggle);
        toggle.SetIsOnWithoutNotify(value);
        toggle.onValueChanged.AddListener(_ => RuntimeUIKit.PlayClick());
        if (onChange != null)
            toggle.onValueChanged.AddListener(onChange);
        return toggle;
    }

    /// <summary>
    /// Thanh trượt: rãnh mảnh, phần đã kéo màu vàng, núm tròn viền vàng, nhãn giá trị nổi phía trên.
    /// format: hàm đổi giá trị ra chữ cho nhãn nổi.
    /// </summary>
    public static Slider Slider(Transform parent, string name, float min, float max, float value, bool whole,
        Func<float, string> format, UnityAction<float> onChange, string minLabel = null, string maxLabel = null)
    {
        RectTransform rt = Rect(parent, name);

        RectTransform track = Rect(rt, "Track");
        track.anchorMin = new Vector2(0f, 0.5f);
        track.anchorMax = new Vector2(1f, 0.5f);
        track.sizeDelta = new Vector2(0f, 4f);
        Image(track, Skin != null ? Skin.fillWhite : null, new Color32(0xF3, 0xE6, 0xBC, 0x90)).pixelsPerUnitMultiplier = 4f;

        RectTransform fillArea = Rect(rt, "Fill Area");
        fillArea.anchorMin = new Vector2(0f, 0.5f);
        fillArea.anchorMax = new Vector2(1f, 0.5f);
        fillArea.sizeDelta = new Vector2(0f, 4f);
        RectTransform fill = Rect(fillArea, "Fill");
        fill.sizeDelta = Vector2.zero;
        Image(fill, Skin != null ? Skin.fillWhite : null, UISkin.GoldBright).pixelsPerUnitMultiplier = 4f;

        RectTransform handleArea = Rect(rt, "Handle Slide Area");
        Stretch(handleArea);
        RectTransform handle = Rect(handleArea, "Handle");
        handle.sizeDelta = new Vector2(24f, 24f);
        Image handleImg = Image(handle, Skin != null ? Skin.sliderKnob : null, Skin != null ? Color.white : UISkin.GoldBright, true);
        handleImg.type = UnityEngine.UI.Image.Type.Simple;
        handleImg.preserveAspect = true;

        // Nhãn giá trị nổi trên núm
        RectTransform bubble = Rect(handle, "Bubble");
        bubble.anchorMin = bubble.anchorMax = new Vector2(0.5f, 1f);
        bubble.pivot = new Vector2(0.5f, 0f);
        bubble.anchoredPosition = new Vector2(0f, 4f);
        bubble.sizeDelta = new Vector2(44f, 20f);
        Image(bubble, Skin != null ? Skin.fillWhite : null, new Color32(0x4A, 0x4A, 0x4A, 0xFF)).pixelsPerUnitMultiplier = 3f;
        TextMeshProUGUI bubbleText = Text(bubble, "Value", "", 12f, UISkin.GoldBright);
        Stretch(bubbleText.rectTransform);

        if (!string.IsNullOrEmpty(minLabel))
        {
            TextMeshProUGUI lo = Text(rt, "Min", minLabel, 12f, UISkin.GoldBright, TextAlignmentOptions.Left);
            lo.rectTransform.anchorMin = lo.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            lo.rectTransform.pivot = new Vector2(0f, 1f);
            lo.rectTransform.anchoredPosition = new Vector2(0f, -14f);
            lo.rectTransform.sizeDelta = new Vector2(60f, 18f);
        }
        if (!string.IsNullOrEmpty(maxLabel))
        {
            TextMeshProUGUI hi = Text(rt, "Max", maxLabel, 12f, UISkin.GoldBright, TextAlignmentOptions.Right);
            hi.rectTransform.anchorMin = hi.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            hi.rectTransform.pivot = new Vector2(1f, 1f);
            hi.rectTransform.anchoredPosition = new Vector2(0f, -14f);
            hi.rectTransform.sizeDelta = new Vector2(60f, 18f);
        }

        Slider slider = rt.gameObject.AddComponent<Slider>();
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImg;
        slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = whole;
        UISkin.SetButtonTint(slider);
        slider.SetValueWithoutNotify(value);
        bubbleText.text = format != null ? format(value) : value.ToString("0");
        slider.onValueChanged.AddListener(v => bubbleText.text = format != null ? format(v) : v.ToString("0"));
        if (onChange != null)
            slider.onValueChanged.AddListener(onChange);
        return slider;
    }

    /// <summary>Dropdown TMP với khung viền vàng + mũi tên chevron.</summary>
    public static TMP_Dropdown Dropdown(Transform parent, string name, float fontSize, float listHeight)
    {
        TMP_DefaultControls.Resources res = new TMP_DefaultControls.Resources();
        GameObject go = TMP_DefaultControls.CreateDropdown(res);
        go.name = name;
        go.transform.SetParent(parent, false);
        SetLayer(go.transform, LayerMask.NameToLayer("UI"));

        TMP_Dropdown dropdown = go.GetComponent<TMP_Dropdown>();
        dropdown.ClearOptions();
        if (Skin != null)
            UISkin.SetSliced(go.GetComponent<Image>(), Skin.boxGold, Color.white);
        UISkin.SetButtonTint(dropdown);

        RectTransform caption = dropdown.captionText.rectTransform;
        caption.offsetMin = new Vector2(18f, 2f);
        caption.offsetMax = new Vector2(-44f, -2f);
        dropdown.captionText.fontSize = fontSize;
        dropdown.captionText.alignment = TextAlignmentOptions.Center;
        UISkin.StyleText(dropdown.captionText, UISkin.Cream);

        RectTransform arrow = (RectTransform)go.transform.Find("Arrow");
        arrow.anchorMin = arrow.anchorMax = new Vector2(1f, 0.5f);
        arrow.anchoredPosition = new Vector2(-26f, 0f);
        arrow.sizeDelta = new Vector2(18f, 18f);
        UISkin.SetIcon(arrow.GetComponent<Image>(), Skin != null ? Skin.chevronDown : null, UISkin.Cream);

        RectTransform template = dropdown.template;
        template.sizeDelta = new Vector2(0f, listHeight);
        if (Skin != null)
            UISkin.SetSliced(template.GetComponent<Image>(), Skin.boxGold, Color.white);
        ((RectTransform)template.Find("Viewport/Content")).sizeDelta = new Vector2(0f, 36f);
        Transform item = template.Find("Viewport/Content/Item");
        ((RectTransform)item).sizeDelta = new Vector2(0f, 32f);
        Toggle itemToggle = item.GetComponent<Toggle>();
        ColorBlock colors = itemToggle.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 0f);
        colors.highlightedColor = new Color32(0x8D, 0x61, 0x00, 0xAA);
        colors.pressedColor = new Color32(0x8D, 0x61, 0x00, 0xFF);
        colors.selectedColor = new Color32(0x8D, 0x61, 0x00, 0x66);
        itemToggle.colors = colors;
        Image itemBg = item.Find("Item Background").GetComponent<Image>();
        itemBg.color = Color.white;
        item.Find("Item Checkmark").GetComponent<Image>().color = UISkin.GoldBright;
        dropdown.itemText.fontSize = fontSize;
        dropdown.itemText.alignment = TextAlignmentOptions.Center;
        UISkin.StyleText(dropdown.itemText, UISkin.Cream);

        Scrollbar scrollbar = template.GetComponentInChildren<Scrollbar>(true);
        if (scrollbar != null)
        {
            scrollbar.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.3f);
            if (Skin != null && scrollbar.handleRect != null)
                UISkin.SetSliced(scrollbar.handleRect.GetComponent<Image>(), Skin.fillWhite, UISkin.Gold, 3f);
        }
        return dropdown;
    }

    private static void SetLayer(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        foreach (Transform child in t)
            SetLayer(child, layer);
    }

    /// <summary>Vùng cuộn dọc; trả về Content (có VerticalLayoutGroup + ContentSizeFitter).</summary>
    public static RectTransform ScrollList(Transform parent, string name, float spacing, RectOffset padding, out RectTransform viewport)
    {
        viewport = Rect(parent, name);
        Image(viewport, null, new Color(1f, 1f, 1f, 0f), true);
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform content = Rect(viewport, "Content");
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;

        VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.padding = padding;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;
        return content;
    }
}
