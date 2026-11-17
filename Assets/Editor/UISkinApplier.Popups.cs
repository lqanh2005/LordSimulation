using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Skin cho các popup có sẵn trong GameplayHUD: Tạm dừng, Luật, Nghiên cứu.</summary>
public static partial class UISkinApplier
{
    [MenuItem("Tools/LordSimulation/UI Skin/Apply Gameplay Popups")]
    public static void ApplyGameplayPopups()
    {
        if (!Begin(GameplayScenePath, out Scene scene))
            return;

        Transform hud = FindRoot(scene, "GameplayHUD");
        if (hud == null)
            return;

        StylePauseMenu(hud);
        StyleEdictPopup(hud);
        StyleResearchPopup(hud);
        StyleGameOverPopup(hud);

        // Hiệu ứng hiện / ẩn (UIPopupAnim): gốc popup = lớp phủ, phóng to khung Window
        AttachPopupAnim(R(hud, "PauseMenu/Overlay"));
        AttachPopupAnim(R(hud, "EdictPopup"));
        AttachPopupAnim(R(hud, "ResearchPopup"));
        AttachPopupAnim(R(hud, "GameOverPopup/Overlay"));

        End(scene);
    }

    // ==========================================
    // THẮNG / THUA (không có mockup riêng — dùng kiểu Pause_Mockup)
    // ==========================================
    private static void StyleGameOverPopup(Transform hud)
    {
        RectTransform overlay = R(hud, "GameOverPopup/Overlay");
        if (overlay == null) return;
        overlay.GetComponent<Image>().color = UISkin.Overlay;

        RectTransform window = R(overlay, "Window");
        const float width = 587f;
        Place(window, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, 560f));
        PopupHeader(window, null, 32f, 30f, 80f);
        Ornament(window, "Ornament_Top", 88f, 0f, 170f);

        TMP_Text reason = T(window, "Txt_Reason");
        PlaceTopLeft(reason.rectTransform, 60f, 104f, width - 120f, 80f);
        StyleTmp(reason, 18f, UISkin.Cream, TextAlignmentOptions.Center);
        reason.textWrappingMode = TextWrappingModes.Normal;

        TMP_Text stats = T(window, "Txt_Stats");
        PlaceTopLeft(stats.rectTransform, 60f, 190f, width - 120f, 30f);
        StyleTmp(stats, 15f, UISkin.Tan, TextAlignmentOptions.Center);

        // Continue và Restart dùng chung một chỗ (chỉ hiện một nút tuỳ thắng/thua)
        (string name, float y)[] buttons = { ("Btn_Continue", 240f), ("Btn_Restart", 240f), ("Btn_Load", 325f), ("Btn_MainMenu", 410f) };
        foreach ((string name, float y) in buttons)
        {
            RectTransform btn = R(window, name);
            if (btn == null) continue;
            PlaceTopLeft(btn, (width - 340f) * 0.5f, y, 340f, 69f);
            OrnateButton(btn, null, 20f);
        }

        // GameOverPopupController tự đổi chiều cao theo thắng/thua -> không đặt gạch đáy cố định
        Transform bottom = window.Find("Ornament_Bottom");
        if (bottom != null)
            Undo.DestroyObjectImmediate(bottom.gameObject);
    }

    // ==========================================
    // NGHIÊN CỨU (Research_Mockup.png 1514x817)
    // ==========================================
    private static void StyleResearchPopup(Transform hud)
    {
        RectTransform overlay = R(hud, "ResearchPopup");
        if (overlay == null) return;
        overlay.GetComponent<Image>().color = UISkin.Overlay;
        ResearchTreePopupController popup = overlay.GetComponent<ResearchTreePopupController>();

        RectTransform window = R(overlay, "Window");
        Place(window, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1514f, 817f));
        PopupHeader(window, "Nghiên cứu", 30f, 38f, 96f);
        Ornament(window, "Ornament_Title", 60f, 190f, 155f);

        // --- Cột trái: nhánh nghiên cứu ---
        RectTransform branchList = Child(window, "BranchList");
        PlaceTopLeft(branchList, 33f, 104f, 220f, 3f * 75f);
        RectTransform branchTemplate = Child(branchList, "Template_Branch");
        PlaceTopLeft(branchTemplate, 0f, 0f, 220f, 65f);
        Image branchImage = Img(branchTemplate);
        GoldBox(branchTemplate);
        Button branchButton = branchTemplate.GetComponent<Button>();
        if (branchButton == null)
            branchButton = branchTemplate.gameObject.AddComponent<Button>();
        branchButton.targetGraphic = branchImage;
        UISkin.SetButtonTint(branchButton);
        TMP_Text branchLabel = Tmp(branchTemplate, "Label", "Nhánh");
        Stretch(branchLabel.rectTransform);
        StyleTmp(branchLabel, 17f, UISkin.Cream, TextAlignmentOptions.Center);
        branchTemplate.gameObject.SetActive(false);

        // --- Dưới cột trái: điểm NC + đang nghiên cứu ---
        RectTransform strip = R(window, "StatusStrip");
        if (strip != null)
        {
            PlaceTopLeft(strip, 33f, 560f, 220f, 216f);
            GoldBox(strip);
            TMP_Text points = T(strip, "Txt_Points");
            PlaceTopLeft(points.rectTransform, 16f, 14f, 188f, 64f);
            StyleTmp(points, 14f, UISkin.Tan, TextAlignmentOptions.TopLeft);
            points.textWrappingMode = TextWrappingModes.Normal;

            TMP_Text current = T(strip, "Txt_Current");
            PlaceTopLeft(current.rectTransform, 16f, 84f, 188f, 92f);
            StyleTmp(current, 14f, UISkin.Cream, TextAlignmentOptions.TopLeft);
            current.textWrappingMode = TextWrappingModes.Normal;

            RectTransform bar = R(strip, "Progress_Bg");
            PlaceTopLeft(bar, 16f, 190f, 188f, 6f);
            UISkin.SetSliced(Img(bar), skin.fillWhite, new Color32(0x5A, 0x52, 0x46, 0xFF), 4f);
            RectTransform fill = R(bar, "Fill");
            if (fill != null)
                UISkin.SetSliced(Img(fill), skin.fillWhite, UISkin.GoldBright, 4f);
        }

        // --- Giữa: cây ---
        RectTransform viewport = R(window, "TreeViewport");
        PlaceTopLeft(viewport, 279f, 104f, 834f, 672f);
        GoldBox(viewport);
        RectTransform content = R(viewport, "Content");
        PlaceTopLeft(content, 0f, 0f, 834f, 672f);

        RectTransform line = R(content, "Template_Line");
        if (line != null)
            Img(line).sprite = null;

        RectTransform node = R(content, "Template_Node");
        if (node != null)
        {
            PlaceTopLeft(node, 0f, 0f, 180f, 64f);
            Img(node).color = Color.clear;
            RectTransform bg = R(node, "Background");
            Stretch(bg);
            UISkin.SetSliced(Img(bg), skin.boxGold, Color.white);

            RectTransform icon = Child(bg, "Icon");
            Place(icon, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(30f, 0f), new Vector2(28f, 28f));
            UISkin.SetIcon(Img(icon), skin.iconAgriculture);
            Img(icon).raycastTarget = false;

            TMP_Text name = T(bg, "Txt_Name");
            Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(52f, -1f), new Vector2(122f, 24f));
            StyleTmp(name, 16f, UISkin.Cream, TextAlignmentOptions.BottomLeft, true);
            name.overflowMode = TextOverflowModes.Ellipsis;

            TMP_Text info = T(bg, "Txt_Info");
            Place(info.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 1f), new Vector2(52f, -1f), new Vector2(122f, 20f));
            StyleTmp(info, 12f, UISkin.Tan, TextAlignmentOptions.TopLeft);

            RectTransform progress = R(bg, "Progress");
            if (progress != null)
            {
                progress.offsetMin = new Vector2(10f, 6f);
                progress.offsetMax = new Vector2(-10f, 9f);
                UISkin.SetSliced(Img(progress), skin.fillWhite, UISkin.GoldBright, 6f);
            }

            ResearchNodeView view = node.GetComponent<ResearchNodeView>();
            if (view != null)
                SetRef(view, "icon", Img(icon));
        }

        // --- Phải: chi tiết ---
        RectTransform detail = R(window, "Detail");
        PlaceTopLeft(detail, 1138f, 104f, 343f, 672f);
        GoldBox(detail);

        TMP_Text dName = T(detail, "Txt_Name");
        StretchTop(dName.rectTransform, 22f, 32f, 20f);
        StyleTmp(dName, 20f, UISkin.GoldBright, TextAlignmentOptions.Center, true);

        RectTransform dIcon = Child(detail, "Icon");
        Place(dIcon, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -66f), new Vector2(64f, 64f));
        UISkin.SetIcon(Img(dIcon), skin.iconAgriculture);
        Img(dIcon).raycastTarget = false;

        TMP_Text dDesc = T(detail, "Txt_Desc");
        StretchTop(dDesc.rectTransform, 140f, 66f, 22f);
        StyleTmp(dDesc, 15f, UISkin.Cream, TextAlignmentOptions.Top);
        dDesc.textWrappingMode = TextWrappingModes.Normal;

        RectTransform divider = Child(detail, "Divider");
        StretchTop(divider, 214f, 1f, 30f);
        Img(divider).color = UISkin.Gold;
        Img(divider).raycastTarget = false;

        TMP_Text dBranch = T(detail, "Txt_Branch");
        StretchTop(dBranch.rectTransform, 228f, 24f, 30f);
        StyleTmp(dBranch, 15f, UISkin.Tan, TextAlignmentOptions.Left);

        TMP_Text dEffects = T(detail, "Txt_Effects");
        StretchTop(dEffects.rectTransform, 262f, 180f, 30f);
        StyleTmp(dEffects, 15f, UISkin.Cream, TextAlignmentOptions.TopLeft);
        dEffects.textWrappingMode = TextWrappingModes.Normal;

        TMP_Text dStatus = T(detail, "Txt_Status");
        StretchBottom(dStatus.rectTransform, 100f, 120f, 30f);
        StyleTmp(dStatus, 13f, UISkin.Tan, TextAlignmentOptions.BottomLeft);
        dStatus.textWrappingMode = TextWrappingModes.Normal;

        RectTransform btn = R(detail, "Btn_Research");
        Place(btn, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(283f, 60f));
        BoxButton(btn, 19f, "Nghiên cứu");

        if (popup != null)
        {
            SetRef(popup, "branchList", branchList);
            SetRef(popup, "branchButtonTemplate", branchButton);
            SetRef(popup, "imgDetailIcon", Img(dIcon));
            SetVector2(popup, "nodeSize", new Vector2(180f, 64f));
            SetFloat(popup, "columnSpacing", 200f);
            SetFloat(popup, "rowSpacing", 98f);
            SetVector2(popup, "padding", new Vector2(24f, 24f));
            SetFloat(popup, "lineThickness", 2f);
            SetColor(popup, "textColor", UISkin.Cream);
            SetColor(popup, "lockedTextColor", UISkin.Muted);
            SetColor(popup, "selectedOutlineColor", UISkin.GoldBright);
            SetColor(popup, "lineColor", new Color32(0x6E, 0x67, 0x58, 0xFF));
            SetColor(popup, "lineDoneColor", UISkin.Gold);
        }
    }

    private static void AttachPopupAnim(RectTransform root)
    {
        if (root == null) return;
        UIPopupAnim anim = root.GetComponent<UIPopupAnim>();
        if (anim == null)
            anim = Undo.AddComponent<UIPopupAnim>(root.gameObject);
        SetRef(anim, "window", R(root, "Window"));
    }

    private static void SetFloat(Object target, string field, float value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty(field);
        if (property == null) return;
        property.floatValue = value;
        so.ApplyModifiedProperties();
    }

    private static void SetVector2(Object target, string field, Vector2 value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty(field);
        if (property == null) return;
        property.vector2Value = value;
        so.ApplyModifiedProperties();
    }

    // ==========================================
    // LUẬT (Rule_Mockup.png 1276x622)
    // ==========================================
    private static void StyleEdictPopup(Transform hud)
    {
        RectTransform overlay = R(hud, "EdictPopup");
        if (overlay == null) return;
        overlay.GetComponent<Image>().color = UISkin.Overlay;

        RectTransform window = R(overlay, "Window");
        Place(window, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1276f, 622f));
        PopupHeader(window, "Luật", 30f, 34f, 92f);
        Ornament(window, "Ornament_Title", 56f, 90f, 155f);

        // --- Tạo luật mới ---
        RectTransform create = R(window, "CreateRulePanel");
        PlaceTopLeft(create, 32f, 100f, 696f, 486f);
        SectionBox(create, "Tạo luật mới");

        RectTransform content = R(create, "Body/Content");
        RectTransform row = R(content, "Conditions/ConditionRow");
        if (row != null)
        {
            RectTransform link = R(row, "Btn_Link");
            PlaceLeftEdge(link, 31f, 82f, 50f);
            Chip(link, true, "Nếu");
            StyleDropdown(row.Find("Dd_Variable")?.GetComponent<TMP_Dropdown>(), 137f, 271f);
            StyleDropdown(row.Find("Dd_Operator")?.GetComponent<TMP_Dropdown>(), 424f, 119f);
            StyleInput(row.Find("Input_Value")?.GetComponent<TMP_InputField>(), 559f, 105f);

            RectTransform remove = R(row, "Btn_Remove");
            if (remove != null)
            {
                PlaceLeftEdge(remove, 669f, 20f, 20f);
                UISkin.SetIcon(Img(remove), skin.closeIcon, new Color(1f, 1f, 1f, 0.8f));
                Hide(remove, "Label");
            }
        }

        RectTransform linkRow = R(content, "LinkRow");
        if (linkRow != null)
        {
            RectTransform and = R(linkRow, "Btn_And");
            PlaceLeftEdge(and, 31f, 70f, 50f);
            Chip(and, true, "Và");
            RectTransform or = R(linkRow, "Btn_Or");
            PlaceLeftEdge(or, 109f, 90f, 50f);
            Chip(or, false, "Hoặc");
            RectTransform add = R(linkRow, "Btn_AddCondition");
            PlaceLeftEdge(add, 223f, 441f, 50f);
            Chip(add, false, "+ Thêm điều kiện");
            StyleTmp(T(add, "Label"), 14f, UISkin.Cream, TextAlignmentOptions.Center);
        }

        RectTransform thenRow = R(content, "ThenRow");
        if (thenRow != null)
        {
            RectTransform then = R(thenRow, "Chip_Then");
            PlaceLeftEdge(then, 31f, 76f, 50f);
            Chip(then, true, "Thì");
            StyleDropdown(thenRow.Find("Dd_Action")?.GetComponent<TMP_Dropdown>(), 131f, 533f);
        }

        RectTransform activate = R(create, "Btn_Activate");
        PlaceTopLeft(activate, 31f, 393f, 633f, 58f);
        BoxButton(activate, 19f, "Kích hoạt");

        TMP_Text status = T(create, "Txt_Status");
        if (status != null)
        {
            PlaceTopLeft(status.rectTransform, 31f, 454f, 633f, 26f);
            StyleTmp(status, 14f, UISkin.Tan, TextAlignmentOptions.Center);
        }

        // --- Luật đang hoạt động ---
        RectTransform active = R(window, "ActiveRulesPanel");
        PlaceTopLeft(active, 752f, 100f, 492f, 486f);
        SectionBox(active, "Luật đang hoạt động");

        TMP_Text empty = T(active, "Txt_Empty");
        StyleTmp(empty, 15f, UISkin.Muted, TextAlignmentOptions.Center);

        RectTransform card = R(active, "List/Content/RuleCard_Template");
        if (card != null)
        {
            GoldBox(card);
            TMP_Text title = T(card, "Txt_Title");
            PlaceTopLeft(title.rectTransform, 24f, 12f, 270f, 32f);
            StyleTmp(title, 16f, UISkin.Cream, TextAlignmentOptions.Left, true);
            title.overflowMode = TextOverflowModes.Ellipsis;

            RectTransform toggle = R(card, "Btn_Toggle");
            PlaceTopLeft(toggle, 312f, 16f, 44f, 24f);
            UISkin.SetIcon(Img(toggle), skin.toggleOn);
            Img(toggle).pixelsPerUnitMultiplier = 1f;
            SetActive(toggle, "Knob", false);

            RectTransform delete = R(card, "Btn_Delete");
            PlaceTopLeft(delete, 380f, 16f, 24f, 24f);
            UISkin.SetIcon(Img(delete), skin.deleteIcon);
            Hide(delete, "Label");

            StyleTmp(T(card, "Txt_Links"), 16f, UISkin.Cream, TextAlignmentOptions.TopLeft, true);
            StyleTmp(T(card, "Txt_Details"), 16f, UISkin.Cream, TextAlignmentOptions.TopLeft, true);

            EdictRuleCardView view = card.GetComponent<EdictRuleCardView>();
            if (view != null)
            {
                SetColor(view, "activeTextColor", UISkin.Cream);
                SetColor(view, "inactiveTextColor", UISkin.Muted);
            }
        }

        EdictPopupController popup = overlay.GetComponent<EdictPopupController>();
        if (popup != null)
        {
            SetColor(popup, "successColor", UISkin.Positive);
            SetColor(popup, "errorColor", UISkin.Negative);
        }
    }

    /// <summary>Vùng nội dung: hộp tối viền vàng, Header trong suốt với tiêu đề kem ở giữa.</summary>
    private static void SectionBox(RectTransform section, string title)
    {
        GoldBox(section);
        RectTransform header = R(section, "Header");
        if (header == null) return;
        Img(header).color = Color.clear;
        TMP_Text text = T(header, "Txt_Title");
        if (text == null) return;
        StretchTop(text.rectTransform, 18f, 34f, 10f);
        StyleTmp(text, 18f, UISkin.Cream, TextAlignmentOptions.Center, true);
        text.text = title;
    }

    /// <summary>Chip trong bảng luật: vàng đậm (đang chọn / nhãn) hoặc tối viền vàng.</summary>
    private static void Chip(RectTransform chip, bool filled, string label)
    {
        if (chip == null) return;
        UISkin.SetSliced(Img(chip), filled ? skin.fillGold : skin.boxGold, Color.white);
        Button button = chip.GetComponent<Button>();
        if (button != null)
        {
            UISkin.SetButtonTint(button);
            ColorBlock colors = button.colors;
            colors.disabledColor = Color.white; // chip "Nếu" bị khoá nhưng vẫn phải sáng
            button.colors = colors;
        }
        TMP_Text text = T(chip, "Label");
        if (text == null) return;
        Stretch(text.rectTransform);
        StyleTmp(text, 16f, UISkin.Cream, TextAlignmentOptions.Center, true);
        text.text = label;
    }

    private static void PlaceLeftEdge(RectTransform rt, float x, float width, float height)
    {
        Place(rt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(width, height));
    }

    private static void StyleDropdown(TMP_Dropdown dropdown, float x, float width)
    {
        if (dropdown == null) return;
        RectTransform rt = (RectTransform)dropdown.transform;
        PlaceLeftEdge(rt, x, width, 50f);
        GoldBox(rt);
        UISkin.SetButtonTint(dropdown);

        if (dropdown.captionText != null)
            StyleTmp(dropdown.captionText, 14f, UISkin.Cream, TextAlignmentOptions.Left);

        RectTransform arrow = R(rt, "Arrow");
        if (arrow != null)
        {
            Place(arrow, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-26f, 0f), new Vector2(18f, 18f));
            UISkin.SetIcon(Img(arrow), skin.chevronDown);
        }

        RectTransform template = dropdown.template;
        if (template == null) return;
        GoldBox(template);
        Transform viewport = template.Find("Viewport");
        if (viewport != null && viewport.GetComponent<Image>() != null)
            viewport.GetComponent<Image>().color = Color.white;

        Transform item = template.Find("Viewport/Content/Item");
        if (item != null)
        {
            Toggle toggle = item.GetComponent<Toggle>();
            if (toggle != null)
            {
                ColorBlock colors = toggle.colors;
                colors.normalColor = new Color(1f, 1f, 1f, 0f);
                colors.highlightedColor = new Color32(0x8D, 0x61, 0x00, 0xAA);
                colors.pressedColor = new Color32(0x8D, 0x61, 0x00, 0xFF);
                colors.selectedColor = new Color32(0x8D, 0x61, 0x00, 0x66);
                toggle.colors = colors;
            }
            Image checkmark = item.Find("Item Checkmark")?.GetComponent<Image>();
            if (checkmark != null)
                checkmark.color = UISkin.GoldBright;
        }
        if (dropdown.itemText != null)
            StyleTmp(dropdown.itemText, 14f, UISkin.Cream, TextAlignmentOptions.Left);

        Scrollbar scrollbar = template.GetComponentInChildren<Scrollbar>(true);
        if (scrollbar != null)
        {
            Image bar = scrollbar.GetComponent<Image>();
            if (bar != null) bar.color = new Color(0f, 0f, 0f, 0.3f);
            if (scrollbar.handleRect != null)
                UISkin.SetSliced(scrollbar.handleRect.GetComponent<Image>(), skin.fillWhite, UISkin.Gold, 3f);
        }
    }

    private static void StyleInput(TMP_InputField input, float x, float width)
    {
        if (input == null) return;
        RectTransform rt = (RectTransform)input.transform;
        PlaceLeftEdge(rt, x, width, 50f);
        GoldBox(rt);
        UISkin.SetButtonTint(input);
        input.caretColor = UISkin.Cream;
        input.selectionColor = new Color32(0x8D, 0x61, 0x00, 0x99);
        if (input.textComponent != null)
            StyleTmp(input.textComponent, 15f, UISkin.Cream, TextAlignmentOptions.Center);
        if (input.placeholder is TMP_Text placeholder)
            StyleTmp(placeholder, 15f, UISkin.Muted, TextAlignmentOptions.Center);
    }

    // ==========================================
    // TẠM DỪNG (Pause_Mockup.png 587x723)
    // ==========================================
    private static void StylePauseMenu(Transform hud)
    {
        RectTransform overlay = R(hud, "PauseMenu/Overlay");
        if (overlay == null) return;
        overlay.GetComponent<Image>().color = UISkin.Overlay;

        RectTransform window = R(overlay, "Window");
        const float width = 587f;
        const float height = 723f;
        Place(window, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, height));
        PopupHeader(window, "Tạm dừng", 32f, 26f, 64f);

        // Gạch trang trí dưới tiêu đề
        Ornament(window, "Ornament_Top", 91f, 0f, 170f);

        string[] buttons = { "Btn_Resume", "Btn_Save", "Btn_Load", "Btn_Settings", "Btn_MainMenu", "Btn_Exit" };
        string[] labels = { "TIẾP TỤC", "LƯU GAME", "TẢI GAME", "CÀI ĐẶT", "TRANG CHỦ", "THOÁT" };
        for (int i = 0; i < buttons.Length; i++)
        {
            RectTransform btn = R(window, buttons[i]);
            if (btn == null) continue;
            PlaceTopLeft(btn, (width - 340f) * 0.5f, 121f + i * 85f, 340f, 69f);
            OrnateButton(btn, labels[i], 20f);
        }

        TMP_Text status = T(window, "Txt_Status");
        if (status != null)
        {
            PlaceTopLeft(status.rectTransform, 60f, 618f, width - 120f, 26f);
            StyleTmp(status, 15f, UISkin.Tan, TextAlignmentOptions.Center);
        }

        Ornament(window, "Ornament_Bottom", 662f, 0f, 170f);

        PauseMenuController menu = hud.GetComponentInChildren<PauseMenuController>(true);
        if (menu != null)
        {
            SetColor(menu, "infoColor", UISkin.Tan);
            SetColor(menu, "successColor", UISkin.Positive);
            SetColor(menu, "errorColor", UISkin.Negative);
        }
    }

    // ==========================================
    // KHỐI POPUP DÙNG CHUNG
    // ==========================================

    /// <summary>
    /// Khung popup chuẩn mockup: nền BackPopup, Header trong suốt, tiêu đề vàng sáng ở giữa
    /// (cách mép trên titleTop), nút X vàng góc phải. Window phải có con Header/Txt_Title, Header/Btn_Close.
    /// </summary>
    private static void PopupHeader(RectTransform window, string title, float titleSize, float titleTop, float headerHeight)
    {
        UISkin.SetSliced(Img(window), skin.popupFrame, Color.white);

        RectTransform header = R(window, "Header");
        if (header == null) return;
        StretchTop(header, 0f, headerHeight);
        header.GetComponent<Image>().color = Color.clear;

        TMP_Text txt = T(header, "Txt_Title");
        if (txt != null)
        {
            StretchTop(txt.rectTransform, titleTop, titleSize + 14f, 120f);
            StyleTmp(txt, titleSize, UISkin.GoldBright, TextAlignmentOptions.Center, true);
            if (!string.IsNullOrEmpty(title))
                txt.text = title;
        }

        RectTransform close = R(header, "Btn_Close");
        if (close != null)
        {
            Place(close, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(-46f, -(titleTop + titleSize * 0.5f + 7f)), new Vector2(30f, 30f));
            UISkin.SetIcon(Img(close), skin.closeIcon);
            Hide(close, "Label");
            UISkin.SetButtonTint(close.GetComponent<Button>());
        }
    }

    /// <summary>
    /// Gạch trang trí "———◇ [gap] ◇———" căn giữa tại toạ độ y (tính từ mép trên cha).
    /// gap = 0 thì chỉ có một hình thoi ở giữa.
    /// </summary>
    private static void Ornament(RectTransform parent, string name, float centerY, float gap, float lineLength)
    {
        RectTransform root = Child(parent, name);
        Place(root, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -centerY), new Vector2(gap + 2f * (lineLength + 14f), 12f));

        float half = gap * 0.5f;
        bool single = gap <= 0f;

        RectTransform leftLine = Child(root, "Line_L");
        Place(leftLine, new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-half - (single ? 8f : 14f), 0f), new Vector2(lineLength, 3f));
        SetDecor(leftLine, skin.titleLine);

        RectTransform rightLine = Child(root, "Line_R");
        Place(rightLine, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(half + (single ? 8f : 14f), 0f), new Vector2(lineLength, 3f));
        SetDecor(rightLine, skin.titleLineRight);

        RectTransform leftGem = Child(root, "Gem_L");
        Place(leftGem, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(single ? 0f : -half - 6f, 0f), new Vector2(9f, 9f));
        SetDecor(leftGem, skin.rhombus);

        RectTransform rightGem = Child(root, "Gem_R");
        rightGem.gameObject.SetActive(!single);
        Place(rightGem, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(half + 6f, 0f), new Vector2(9f, 9f));
        SetDecor(rightGem, skin.rhombus);
    }

    private static void SetDecor(RectTransform rt, Sprite sprite)
    {
        Image image = Img(rt);
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.color = Color.white;
        image.raycastTarget = false;
    }

    /// <summary>Nút vát hai đầu (Frame_1) + nhãn đậm màu kem.</summary>
    private static void OrnateButton(RectTransform btn, string label, float fontSize)
    {
        UISkin.SetSliced(Img(btn), skin.ornateButton, Color.white);
        Button button = btn.GetComponent<Button>();
        UISkin.SetButtonTint(button);
        if (button != null)
        {
            SpriteState state = button.spriteState;
            state.highlightedSprite = skin.ornateButtonSelected;
            state.pressedSprite = skin.ornateButtonSelected;
            state.disabledSprite = skin.ornateButtonDisabled;
            button.spriteState = state;
            button.transition = Selectable.Transition.SpriteSwap;
        }

        TMP_Text text = T(btn, "Label");
        if (text == null) return;
        Stretch(text.rectTransform);
        StyleTmp(text, fontSize, UISkin.Cream, TextAlignmentOptions.Center, true);
        text.fontStyle = FontStyles.Bold;
        if (!string.IsNullOrEmpty(label))
            text.text = label;
    }

    /// <summary>Hộp nền tối viền vàng mảnh (vùng nội dung trong popup).</summary>
    private static void GoldBox(RectTransform rt, bool bright = false)
    {
        UISkin.SetSliced(Img(rt), bright ? skin.boxGoldBright : skin.boxGold, Color.white);
    }

    /// <summary>Nút chữ dạng hộp viền vàng (Kích hoạt, Nghiên cứu, Lưu game...).</summary>
    private static void BoxButton(RectTransform btn, float fontSize, string label = null)
    {
        GoldBox(btn);
        UISkin.SetButtonTint(btn.GetComponent<Button>());
        TMP_Text text = T(btn, "Label");
        if (text == null) return;
        Stretch(text.rectTransform);
        StyleTmp(text, fontSize, UISkin.Cream, TextAlignmentOptions.Center, true);
        if (!string.IsNullOrEmpty(label))
            text.text = label;
    }
}
