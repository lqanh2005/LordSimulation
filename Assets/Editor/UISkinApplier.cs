using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Áp skin mockup (Assets/UI) lên UI đã có trong scene — sửa tại chỗ, không xoá/tạo lại
/// nên giữ nguyên các tham chiếu và listener đã gắn. Chạy lại nhiều lần vẫn an toàn.
/// Toạ độ lấy theo mockup 1920x1080.
/// </summary>
public static partial class UISkinApplier
{
    private const string GameplayScenePath = "Assets/Scenes/GamePlayScene.unity";

    // Viền khung vát góc (BackPopup) thu nhỏ cho HUD: 50px gốc / 4 = góc ~12px.
    private const float HudFramePpu = 4f;

    private static UISkin skin;

    [MenuItem("Tools/LordSimulation/UI Skin/Apply All")]
    public static void ApplyAll()
    {
        ApplyHomeScene();
        ApplyLoadingScene();
        ApplyGameplayHUD();
        ApplyGameplayPopups();
    }

    // ==========================================
    // GAMEPLAY HUD (GamePlay_Mockup.png)
    // ==========================================
    [MenuItem("Tools/LordSimulation/UI Skin/Apply Gameplay HUD")]
    public static void ApplyGameplayHUD()
    {
        if (!Begin(GameplayScenePath, out Scene scene))
            return;

        Transform hud = FindRoot(scene, "GameplayHUD");
        if (hud == null)
        {
            Debug.LogError("[UISkin] Không thấy GameplayHUD trong GamePlayScene.");
            return;
        }

        StyleSpeedControls(hud);
        StyleSeasonClock(hud);
        StyleResources(hud);
        StyleTopRight(hud);
        StyleCityStatus(hud);
        StyleNotifications(hud);
        StyleRightMenu(hud);
        StyleBuildBar(hud);

        HUDButtonBinder binder = hud.GetComponent<HUDButtonBinder>();
        if (binder == null)
            binder = Undo.AddComponent<HUDButtonBinder>(hud.gameObject);
        SetRef(binder, "btnGate", R(hud, "Btn_Cống")?.GetComponent<Button>());
        SetRef(binder, "btnNotices", R(hud, "Btn_Thông báo")?.GetComponent<Button>());

        End(scene);
    }

    private static void StyleSpeedControls(Transform hud)
    {
        RectTransform group = R(hud, "SpeedControls");
        if (group == null) return;
        PlaceTopLeft(group, 34f, 26f, 258f, 62f);

        RectTransform pause = R(group, "Btn_Pause");
        PlaceTopLeft(pause, 0f, 0f, 62f, 62f);
        SquareButton(pause, skin.iconPause, 16f, 24f);
        Hide(pause, "Label");

        RectTransform panel = R(group, "SpeedPanel");
        PlaceTopLeft(panel, 72f, 0f, 186f, 62f);
        UISkin.SetSliced(panel.GetComponent<Image>(), skin.popupFrame, Color.white, HudFramePpu);

        for (int i = 0; i < 3; i++)
        {
            RectTransform btn = R(panel, "Btn_x" + (i + 1));
            if (btn == null) continue;
            Place(btn, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(17f + i * 58f, 0f), new Vector2(42f, 42f));
            UISkin.SetSliced(btn.GetComponent<Image>(), i == 0 ? skin.squareButtonActive : skin.squareButton, Color.white);
            UISkin.SetButtonTint(btn.GetComponent<Button>());
            Label(btn, "Label", 18f, UISkin.Cream);
        }

        GameSpeedHUDController speed = group.GetComponent<GameSpeedHUDController>();
        if (speed != null)
        {
            SetColor(speed, "normalColor", Color.white);
            SetColor(speed, "activeColor", Color.white);
        }
    }

    private static void StyleSeasonClock(Transform hud)
    {
        RectTransform panel = R(hud, "SeasonClock");
        if (panel == null) return;

        const float seasonWidth = 220f;
        const float resourcesWidth = 900f;
        float left = -(seasonWidth + 10f + resourcesWidth) * 0.5f;
        Place(panel, new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(left, -26f), new Vector2(seasonWidth, 62f));
        UISkin.SetSliced(panel.GetComponent<Image>(), skin.popupFrame, Color.white, HudFramePpu);

        SetActive(panel, "RingRoot", false);
        SetActive(panel, "Txt_MonthAndYear", false);

        RectTransform icon = Child(panel, "Icon_Season");
        Place(icon, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(36f, 0f), new Vector2(32f, 32f));
        UISkin.SetIcon(Img(icon), skin.iconSun);
        Img(icon).raycastTarget = false;

        TMP_Text title = T(panel, "Txt_DayAndSeason");
        Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(62f, -12f), new Vector2(150f, 22f));
        StyleTmp(title, 16f, UISkin.Cream, TextAlignmentOptions.Left);
        title.text = "Mùa Xuân, Năm 1";

        RectTransform barBg = Child(panel, "SeasonProgress");
        Place(barBg, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(62f, -40f), new Vector2(136f, 5f));
        UISkin.SetSliced(Img(barBg), skin.fillWhite, new Color32(0x5A, 0x52, 0x46, 0xFF), 4f);
        Img(barBg).raycastTarget = false;

        RectTransform fillRt = Child(barBg, "Fill");
        Stretch(fillRt);
        Image fill = Img(fillRt);
        fill.sprite = skin.fillWhite;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;
        fill.fillAmount = 0.6f;
        fill.color = UISkin.Cream;
        fill.raycastTarget = false;

        SeasonClockHUD clock = panel.GetComponent<SeasonClockHUD>();
        if (clock != null)
        {
            SetRef(clock, "txtDayAndSeason", null);
            SetRef(clock, "txtMonthAndYear", null);
            SetRef(clock, "txtSeasonAndYear", title);
            SetRef(clock, "seasonProgressFill", fill);
        }
    }

    private static readonly string[] ResourceItems = { "Res_Wood", "Res_Iron", "Res_Food", "Res_Coal", "Res_Gold", "Res_Medicine" };

    private static void StyleResources(Transform hud)
    {
        RectTransform panel = R(hud, "ResourcesPanel");
        if (panel == null) return;

        const float seasonWidth = 220f;
        const float width = 900f;
        float left = -(seasonWidth + 10f + width) * 0.5f + seasonWidth + 10f;
        Place(panel, new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(left, -26f), new Vector2(width, 62f));
        UISkin.SetSliced(panel.GetComponent<Image>(), skin.popupFrame, Color.white, HudFramePpu);

        Sprite[] icons = { skin.resWood, skin.resStone, skin.resFood, skin.resCoal, skin.resGold, skin.resMedicine };
        const float pad = 12f;
        float itemWidth = (width - pad * 2f) / ResourceItems.Length;

        int separator = 0;
        foreach (Transform child in panel)
        {
            if (child.name != "Separator") continue;
            separator++;
            RectTransform sep = (RectTransform)child;
            Place(sep, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(pad + separator * itemWidth, 0f), new Vector2(1f, 30f));
            sep.GetComponent<Image>().color = new Color32(0x6E, 0x58, 0x38, 0xFF);
        }

        for (int i = 0; i < ResourceItems.Length; i++)
        {
            RectTransform item = R(panel, ResourceItems[i]);
            if (item == null) continue;
            Place(item, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(pad + i * itemWidth, 0f), new Vector2(itemWidth, 44f));

            RectTransform icon = R(item, "Icon");
            Place(icon, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(30f, 0f), new Vector2(32f, 28f));
            UISkin.SetIcon(icon.GetComponent<Image>(), icons[i]);

            SetActive(item, "Txt_Name", false);

            TMP_Text value = T(item, "Txt_Value");
            Place(value.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(54f, 0f), new Vector2(52f, 30f));
            StyleTmp(value, 18f, UISkin.Cream, TextAlignmentOptions.Left);

            TMP_Text rate = Tmp(item, "Txt_Rate", "0/th");
            rate.raycastTarget = false;
            rate.textWrappingMode = TextWrappingModes.NoWrap;
            Place(rate.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(104f, 0f), new Vector2(itemWidth - 106f, 30f));
            StyleTmp(rate, 12f, UISkin.Positive, TextAlignmentOptions.Left);

            TopBarHUDController bar = hud.GetComponent<TopBarHUDController>();
            if (bar != null)
                SetRef(bar, "txt" + ResourceItems[i].Substring(4) + "Rate", rate);
        }

        TopBarHUDController topBar = hud.GetComponent<TopBarHUDController>();
        if (topBar != null)
        {
            SetColor(topBar, "rateColor", new Color32(0x6F, 0xCF, 0x7A, 0xFF));
            SetColor(topBar, "zeroRateColor", UISkin.Muted);
        }
    }

    private static void StyleTopRight(Transform hud)
    {
        (string name, Sprite icon)[] buttons =
        {
            ("Cài đặt", skin.iconSetting),
            ("Thư", skin.iconLetter),
            ("Thông báo", skin.iconNotice)
        };

        for (int i = 0; i < buttons.Length; i++)
        {
            RectTransform btn = R(hud, "Btn_" + buttons[i].name);
            if (btn == null) continue;
            Place(btn, Vector2.one, Vector2.one, new Vector2(-33f - i * 72f, -26f), new Vector2(62f, 62f));
            SquareButton(btn, buttons[i].icon, 26f, 26f);
            SetActive(hud, "Label_" + buttons[i].name, false);
        }
    }

    private static readonly string[] StatusRows = { "Row_Population", "Row_Infection", "Row_Happiness", "Row_Riot", "Row_Reputation" };

    private static void StyleCityStatus(Transform hud)
    {
        RectTransform panel = R(hud, "CityStatusPanel");
        if (panel == null) return;

        const float width = 236f;
        const float headerHeight = 44f;
        const float rowStride = 38f;
        float height = headerHeight + 10f + StatusRows.Length * rowStride + 8f;
        PlaceTopLeft(panel, 34f, 120f, width, height);
        UISkin.SetSliced(panel.GetComponent<Image>(), skin.popupFrame, Color.white, HudFramePpu);

        RectTransform header = R(panel, "Header");
        PlaceTopLeft(header, 0f, 0f, width, headerHeight);
        header.GetComponent<Image>().color = Color.clear;
        RectTransform line = Child(header, "Line");
        Place(line, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width - 24f, 1f));
        Img(line).color = new Color32(0x6E, 0x58, 0x38, 0xFF);
        Img(line).raycastTarget = false;

        TMP_Text title = T(header, "Txt_Title");
        StyleTmp(title, 17f, UISkin.Cream, TextAlignmentOptions.Center, true);

        Sprite[] icons = { skin.statPopulation, skin.statDisease, skin.statStability, skin.statRiot, skin.statReputation };
        Color[] valueColors = { UISkin.Positive, UISkin.Negative, UISkin.Positive, UISkin.Cream, UISkin.Tan };
        for (int i = 0; i < StatusRows.Length; i++)
        {
            RectTransform row = R(panel, StatusRows[i]);
            if (row == null) continue;
            PlaceTopLeft(row, 22f, headerHeight + 10f + i * rowStride, width - 36f, 32f);

            RectTransform icon = R(row, "Icon");
            Place(icon, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(22f, 22f));
            UISkin.SetIcon(icon.GetComponent<Image>(), icons[i]);

            TMP_Text label = T(row, "Txt_Label");
            Place(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(34f, 0f), new Vector2(96f, 30f));
            StyleTmp(label, 17f, UISkin.Cream, TextAlignmentOptions.Left);

            TMP_Text value = T(row, "Txt_Value");
            Place(value.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(132f, 0f), new Vector2(70f, 30f));
            StyleTmp(value, 17f, valueColors[i], TextAlignmentOptions.Left);
        }
    }

    private static void StyleNotifications(Transform hud)
    {
        RectTransform group = R(hud, "Notifications");
        if (group == null) return;

        RectTransform status = R(hud, "CityStatusPanel");
        float top = status != null ? -status.anchoredPosition.y + status.sizeDelta.y + 18f : 400f;
        PlaceTopLeft(group, 34f, top, 720f, 300f);

        string[] notices = { "Notice_Reminder", "Notice_Warning" };
        for (int i = 0; i < notices.Length; i++)
        {
            RectTransform item = R(group, notices[i]);
            if (item == null) continue;
            PlaceTopLeft(item, 0f, i * 64f, i == 0 ? 222f : 304f, 52f);
            UISkin.SetSliced(item.GetComponent<Image>(), skin.popupFrame, Color.white, HudFramePpu);

            SetActive(item, "Icon", false);

            TMP_Text text = T(item, "Txt_Message");
            Place(text.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(item.sizeDelta.x - 78f, 30f));
            StyleTmp(text, 16f, UISkin.Cream, TextAlignmentOptions.Left);

            RectTransform close = R(item, "Btn_Close");
            if (close != null)
            {
                Place(close, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(22f, 22f));
                UISkin.SetIcon(close.GetComponent<Image>(), skin.closeIcon, new Color(1f, 1f, 1f, 0.7f));
                Hide(close, "Label");
            }
        }
    }

    private static void StyleRightMenu(Transform hud)
    {
        (string name, Sprite icon)[] buttons =
        {
            ("Cống", skin.iconGate),
            ("Nghiên cứu", skin.iconResearch),
            ("Luật", skin.iconLaw),
            ("Thuế", skin.iconTax)
        };

        for (int i = 0; i < buttons.Length; i++)
        {
            RectTransform btn = R(hud, "Btn_" + buttons[i].name);
            if (btn == null) continue;
            Place(btn, Vector2.one, Vector2.one, new Vector2(-33f, -193f - i * 72f), new Vector2(62f, 62f));
            SquareButton(btn, buttons[i].icon, 30f, 30f);
            SetActive(hud, "Label_" + buttons[i].name, false);
        }
    }

    // Bỏ nhóm "Xây dựng" (gộp mọi công trình) và "Trang trí" — chỉ giữ nhóm theo loại
    private static readonly string[] BuildButtons = { "Nhà ở", "Sản xuất", "Nông nghiệp", "Hạ tầng", "Phá dỡ" };
    private static readonly string[] RemovedBuildButtons = { "Xây dựng", "Trang trí" };   // Trang trí chỉ có Xe goòng, nay gộp vào Mỏ

    private const float BuildButtonSize = 66f;
    private const float BuildLabelHeight = 18f;

    private static void StyleBuildBar(Transform hud)
    {
        RectTransform bar = R(hud, "BuildBar");
        if (bar == null) return;

        foreach (string removed in RemovedBuildButtons)
        {
            RectTransform old = R(bar, "Btn_" + removed);
            if (old != null)
                Undo.DestroyObjectImmediate(old.gameObject);
        }

        const float size = BuildButtonSize;
        const float spacing = 16f;        // rộng hơn để chữ dưới icon không chạm nhau
        const float padX = 22f;
        const float padTop = 14f;
        const float labelGap = 3f;
        const float padBottom = 10f;
        float width = padX * 2f + BuildButtons.Length * size + (BuildButtons.Length - 1) * spacing;
        float height = padTop + size + labelGap + BuildLabelHeight + padBottom;
        Place(bar, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(width, height));
        UISkin.SetSliced(bar.GetComponent<Image>(), skin.popupFrame, Color.white, 3f);

        Sprite[] icons = { skin.iconHouse, skin.iconProduct, skin.iconAgriculture, skin.iconInfrastructure, skin.deleteIcon };
        for (int i = 0; i < BuildButtons.Length; i++)
        {
            RectTransform btn = R(bar, "Btn_" + BuildButtons[i]);
            if (btn == null) continue;
            Place(btn, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(padX + i * (size + spacing), -padTop), new Vector2(size, size));
            SquareButton(btn, icons[i], 34f, 34f);

            // Tên nhóm nằm ngay dưới nút (ngoài khung nút)
            TMP_Text label = Tmp(btn, "Label", BuildButtons[i]);
            Undo.RecordObject(label, "Apply UI Skin");
            label.text = BuildButtons[i];
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -labelGap), new Vector2(size + spacing, BuildLabelHeight));
            StyleTmp(label, 14f, UISkin.Cream, TextAlignmentOptions.Center);
            SetActive(btn, "Label", true);
        }

        BuildMenuController menu = bar.GetComponent<BuildMenuController>();
        if (menu != null)
            SetColor(menu, "demolishActiveColor", Color.white);

        StyleBuildOptions(hud, bar);
    }

    private static void StyleBuildOptions(Transform hud, RectTransform bar)
    {
        RectTransform options = R(hud, "BuildOptions");
        if (options == null) return;

        // Bảng công trình nằm ngay trên thanh xây dựng (thanh cao hơn vì có chữ)
        Undo.RecordObject(options, "Apply UI Skin");
        options.anchoredPosition = new Vector2(options.anchoredPosition.x, bar.anchoredPosition.y + bar.sizeDelta.y + 12f);

        UISkin.SetSliced(options.GetComponent<Image>(), skin.popupFrame, Color.white, HudFramePpu);
        GridLayoutGroup grid = options.GetComponent<GridLayoutGroup>();
        if (grid != null)
        {
            grid.padding = new RectOffset(18, 18, 18, 18);
            grid.spacing = new Vector2(10f, 10f);
            grid.cellSize = new Vector2(190f, 64f);
        }

        RectTransform template = R(options, "Option_Template");
        if (template == null) return;
        UISkin.SetSliced(template.GetComponent<Image>(), skin.boxGold, Color.white);
        UISkin.SetButtonTint(template.GetComponent<Button>());
        TMP_Text name = T(template, "Txt_Name");
        TMP_Text cost = T(template, "Txt_Cost");
        StretchTop(name.rectTransform, 9f, 26f, 8f);
        StretchBottom(cost.rectTransform, 9f, 20f, 8f);
        StyleTmp(name, 17f, UISkin.Cream, TextAlignmentOptions.Center);
        StyleTmp(cost, 13f, UISkin.Tan, TextAlignmentOptions.Center);

        BuildOptionView view = template.GetComponent<BuildOptionView>();
        if (view != null)
        {
            SetColor(view, "affordableCostColor", UISkin.Tan);
            SetColor(view, "missingCostColor", UISkin.Negative);
        }
    }

    // ==========================================
    // KHỐI DÙNG CHUNG
    // ==========================================

    /// <summary>Nút vuông HUD: nền tối viền vàng + icon giữa.</summary>
    private static void SquareButton(RectTransform btn, Sprite icon, float iconWidth, float iconHeight)
    {
        UISkin.SetSliced(btn.GetComponent<Image>(), skin.squareButton, Color.white);
        UISkin.SetButtonTint(btn.GetComponent<Button>());

        RectTransform iconRt = Child(btn, "Icon");
        Place(iconRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(iconWidth, iconHeight));
        UISkin.SetIcon(Img(iconRt), icon);
        Img(iconRt).raycastTarget = false;
    }

    private static void Label(Transform parent, string name, float size, Color color)
    {
        TMP_Text text = T(parent, name);
        if (text != null)
            StyleTmp(text, size, color, TextAlignmentOptions.Center);
    }

    private static void StyleTmp(TMP_Text text, float size, Color color, TextAlignmentOptions align, bool title = false)
    {
        if (text == null) return;
        Undo.RecordObject(text, "Apply UI Skin");
        text.fontSize = size;
        text.alignment = align;
        text.fontStyle = FontStyles.Normal;
        UISkin.StyleText(text, color, title);
        EditorUtility.SetDirty(text);
    }

    // ==========================================
    // HELPER
    // ==========================================

    private static bool Begin(string scenePath, out Scene scene)
    {
        skin = UISkinSetup.LoadSkin();
        scene = SceneManager.GetSceneByPath(scenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return false;
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }
        return scene.IsValid();
    }

    private static void End(Scene scene)
    {
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[UISkin] Đã áp skin cho " + scene.name);
    }

    private static Transform FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == name)
                return root.transform;
        return null;
    }

    private static RectTransform R(Transform parent, string path)
    {
        return parent != null ? parent.Find(path) as RectTransform : null;
    }

    private static TMP_Text T(Transform parent, string path)
    {
        Transform t = parent != null ? parent.Find(path) : null;
        return t != null ? t.GetComponent<TMP_Text>() : null;
    }

    /// <summary>Lấy con theo tên, chưa có thì tạo (RectTransform, layer UI).</summary>
    private static RectTransform Child(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
            return (RectTransform)existing;

        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(go, "Apply UI Skin");
        return (RectTransform)go.transform;
    }

    /// <summary>Lấy TMP con theo tên, chưa có thì tạo.</summary>
    private static TMP_Text Tmp(Transform parent, string name, string defaultText)
    {
        TMP_Text existing = T(parent, name);
        if (existing != null)
            return existing;
        RectTransform rt = Child(parent, name);
        TextMeshProUGUI tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = defaultText;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static Image Img(RectTransform rt)
    {
        Image image = rt.GetComponent<Image>();
        return image != null ? image : rt.gameObject.AddComponent<Image>();
    }

    private static void Hide(Transform parent, string path) => SetActive(parent, path, false);

    private static void SetActive(Transform parent, string path, bool active)
    {
        Transform t = parent != null ? parent.Find(path) : null;
        if (t != null && t.gameObject.activeSelf != active)
        {
            Undo.RecordObject(t.gameObject, "Apply UI Skin");
            t.gameObject.SetActive(active);
        }
    }

    private static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        if (rt == null) return;
        Undo.RecordObject(rt, "Apply UI Skin");
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    /// <summary>Đặt theo toạ độ mockup: (x, y) là góc trái trên, tính từ góc trái trên của cha.</summary>
    private static void PlaceTopLeft(RectTransform rt, float x, float y, float width, float height)
    {
        Place(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(width, height));
    }

    /// <summary>Kéo ngang theo cha, dính mép trên, cách mép trên `top`, cao `height`.</summary>
    private static void StretchTop(RectTransform rt, float top, float height, float sideInset = 0f)
    {
        if (rt == null) return;
        Undo.RecordObject(rt, "Apply UI Skin");
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(sideInset, -top - height);
        rt.offsetMax = new Vector2(-sideInset, -top);
    }

    private static void StretchBottom(RectTransform rt, float bottom, float height, float sideInset = 0f)
    {
        if (rt == null) return;
        Undo.RecordObject(rt, "Apply UI Skin");
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.offsetMin = new Vector2(sideInset, bottom);
        rt.offsetMax = new Vector2(-sideInset, bottom + height);
    }

    private static void Stretch(RectTransform rt, float inset = 0f)
    {
        Undo.RecordObject(rt, "Apply UI Skin");
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    private static void SetRef(Object target, string field, Object value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty(field);
        if (property == null)
        {
            Debug.LogWarning($"[UISkin] {target.GetType().Name} không có field {field}");
            return;
        }
        property.objectReferenceValue = value;
        so.ApplyModifiedProperties();
    }

    private static void SetColor(Object target, string field, Color value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty(field);
        if (property == null) return;
        property.colorValue = value;
        so.ApplyModifiedProperties();
    }
}
