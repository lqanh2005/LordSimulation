using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class GameplayHUDBuilder
{
    private const string RootName = "GameplayHUD";
    private const string GameplayScenePath = "Assets/Scenes/GamePlayScene.unity";

    private static readonly Color PanelColor = new Color32(0xE2, 0xE5, 0xE7, 0xFF);
    private static readonly Color ButtonColor = new Color32(0xD3, 0xD7, 0xDA, 0xFF);
    private static readonly Color IconColor = new Color32(0x8C, 0x8C, 0x8C, 0xFF);
    private static readonly Color TextColor = new Color32(0x33, 0x33, 0x33, 0xFF);
    private static readonly Color SubTextColor = new Color32(0x6B, 0x6F, 0x73, 0xFF);
    private static readonly Color HeaderColor = new Color32(0x9A, 0x9A, 0x9A, 0xFF);
    private static readonly Color SeparatorColor = new Color32(0xBF, 0xC3, 0xC6, 0xFF);
    private static readonly Color PositiveColor = new Color32(0x4C, 0xAF, 0x50, 0xFF);
    private static readonly Color NegativeColor = new Color32(0xE5, 0x48, 0x48, 0xFF);
    private static readonly Color ReminderColor = new Color32(0xFF, 0xF4, 0xDC, 0xFF);
    private static readonly Color WarningColor = new Color32(0xFD, 0xE0, 0xE0, 0xFF);

    private static Sprite panelSprite;
    private static Sprite circleSprite;

    [MenuItem("Tools/LordSimulation/Build Gameplay HUD")]
    public static void Build()
    {
        panelSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        circleSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

        if (!TryOpenGameplayScene(out Scene scene))
            return;

        GameObject existing = FindRoot(scene, RootName);
        if (existing != null)
        {
            if (!EditorUtility.DisplayDialog("Gameplay HUD", "GamePlayScene đã có GameplayHUD. Xoá và tạo lại?", "Tạo lại", "Huỷ"))
                return;
            Undo.DestroyObjectImmediate(existing);
        }

        GameObject root = CreateCanvas();
        SceneManager.MoveGameObjectToScene(root, scene);
        Undo.RegisterCreatedObjectUndo(root, "Build Gameplay HUD");
        EnsureEventSystem(scene);

        Transform parent = root.transform;
        TopBarHUDController topBar = root.AddComponent<TopBarHUDController>();

        BuildSpeedControls(parent);
        BuildSeasonClock(parent);
        BuildResources(parent, topBar);
        BuildTopRightButtons(parent);
        BuildCityStatus(parent, topBar);
        BuildNotifications(parent);
        BuildRightMenu(parent);
        BuildBottomBar(parent);

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    [MenuItem("Tools/LordSimulation/Add Season Ring To Existing HUD")]
    public static void AddSeasonRingToExistingHUD()
    {
        if (!TryOpenGameplayScene(out Scene scene))
            return;

        TopBarHUDController topBar = FindInScene<TopBarHUDController>(scene);
        if (topBar == null)
        {
            EditorUtility.DisplayDialog("Season Ring", "Không tìm thấy TopBarHUDController trong GamePlayScene.", "OK");
            return;
        }

        SerializedObject topBarSo = new SerializedObject(topBar);
        SerializedProperty dateProperty = topBarSo.FindProperty("txtDateAndSeason");
        TMP_Text txtDate = dateProperty.objectReferenceValue as TMP_Text;
        Image seasonIcon = topBarSo.FindProperty("imgSeasonIcon").objectReferenceValue as Image;

        SeasonClockHUD clock = FindInScene<SeasonClockHUD>(scene);
        if (txtDate == null && clock != null)
            txtDate = new SerializedObject(clock).FindProperty("txtDayAndSeason").objectReferenceValue as TMP_Text;

        RectTransform anchorRect = seasonIcon != null ? seasonIcon.rectTransform : txtDate != null ? txtDate.rectTransform : null;
        RectTransform group = anchorRect != null ? anchorRect.parent as RectTransform : null;
        if (group == null)
        {
            EditorUtility.DisplayDialog("Season Ring", "TopBarHUDController chưa gắn imgSeasonIcon hoặc txtDateAndSeason.", "OK");
            return;
        }

        circleSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

        Transform oldRing = group.Find("SeasonRingRoot");
        if (oldRing != null)
            Undo.DestroyObjectImmediate(oldRing.gameObject);

        if (clock == null || clock.transform != group)
        {
            if (clock != null)
                Undo.DestroyObjectImmediate(clock);
            clock = Undo.AddComponent<SeasonClockHUD>(group.gameObject);
        }

        const float ringSize = 58f;
        const float iconSize = 32f;

        RectTransform ringRoot = CreateRect("SeasonRingRoot", group);
        ringRoot.anchorMin = anchorRect.anchorMin;
        ringRoot.anchorMax = anchorRect.anchorMax;
        ringRoot.pivot = new Vector2(0.5f, 0.5f);
        ringRoot.anchoredPosition = seasonIcon != null ? seasonIcon.rectTransform.anchoredPosition : Vector2.zero;
        ringRoot.sizeDelta = new Vector2(ringSize, ringSize);
        ringRoot.SetSiblingIndex(anchorRect.GetSiblingIndex());

        RectTransform ringRect = CreateRect("SeasonRing", ringRoot);
        Stretch(ringRect);
        SeasonRingGraphic ring = ringRect.gameObject.AddComponent<SeasonRingGraphic>();
        ring.raycastTarget = false;
        SerializedObject ringSo = new SerializedObject(ring);
        ringSo.FindProperty("thickness").floatValue = 7f;
        ringSo.ApplyModifiedPropertiesWithoutUndo();

        RectTransform pointer = CreateRect("Pointer", ringRoot);
        Place(pointer, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(10f, 10f));
        pointer.localRotation = Quaternion.Euler(0f, 0f, 45f);
        AddImage(pointer, TextColor, null).raycastTarget = false;

        Undo.RegisterCreatedObjectUndo(ringRoot.gameObject, "Add Season Ring");

        if (seasonIcon != null)
        {
            Undo.RecordObject(seasonIcon.rectTransform, "Add Season Ring");
            seasonIcon.rectTransform.sizeDelta = new Vector2(iconSize, iconSize);
        }

        if (txtDate != null && seasonIcon != null)
        {
            Undo.RecordObject(txtDate.rectTransform, "Add Season Ring");
            RectTransform textRect = txtDate.rectTransform;
            float textX = ringRoot.anchoredPosition.x + ringSize * 0.5f + 8f + textRect.sizeDelta.x * textRect.pivot.x;
            textRect.anchoredPosition = new Vector2(textX, textRect.anchoredPosition.y);
        }

        SetRef(clock, "ringPivot", ringRect);
        SetRef(clock, "ring", ring);
        SetRef(clock, "txtDayAndSeason", txtDate);

        dateProperty.objectReferenceValue = null;
        topBarSo.ApplyModifiedProperties();

        EnsureEventSystem(scene);

        Selection.activeGameObject = group.gameObject;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component != null)
                return component;
        }
        return null;
    }

    private static GameObject CreateCanvas()
    {
        GameObject root = new GameObject(RootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.layer = LayerMask.NameToLayer("UI");

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        return root;
    }

    private static bool TryOpenGameplayScene(out Scene scene)
    {
        scene = SceneManager.GetSceneByPath(GameplayScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return false;
            scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
        }

        if (!scene.IsValid())
            return false;

        SceneManager.SetActiveScene(scene);
        return true;
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == name)
                return root;
        }
        return null;
    }

    private static void EnsureEventSystem(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.GetComponentInChildren<EventSystem>(true) != null)
                return;
        }

        GameObject go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        go.AddComponent<StandaloneInputModule>();
#endif
        SceneManager.MoveGameObjectToScene(go, scene);
        Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
    }

    // ==========================================
    // GÓC TRÁI TRÊN: TẠM DỪNG + TỐC ĐỘ
    // ==========================================
    private static void BuildSpeedControls(Transform parent)
    {
        RectTransform group = CreateRect("SpeedControls", parent);
        Place(group, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -34f), new Vector2(256f, 70f));
        GameSpeedHUDController speed = group.gameObject.AddComponent<GameSpeedHUDController>();

        RectTransform pausePanel = CreatePanel("Btn_Pause", group, ButtonColor);
        Place(pausePanel, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(70f, 70f));
        Button btnPause = AddButton(pausePanel);
        TextMeshProUGUI pauseText = CreateText("Label", pausePanel, "II", 32f, TextColor, TextAlignmentOptions.Center);
        pauseText.fontStyle = FontStyles.Bold;
        Stretch(pauseText.rectTransform);

        RectTransform speedPanel = CreatePanel("SpeedPanel", group, PanelColor);
        Place(speedPanel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(80f, 0f), new Vector2(176f, 70f));

        Button[] speedButtons = new Button[3];
        for (int i = 0; i < 3; i++)
        {
            RectTransform btn = CreatePanel($"Btn_x{i + 1}", speedPanel, ButtonColor);
            Place(btn, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f + i * 56f, 0f), new Vector2(46f, 46f));
            speedButtons[i] = AddButton(btn);
            TextMeshProUGUI label = CreateText("Label", btn, $"x{i + 1}", 20f, TextColor, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
        }

        SetRef(speed, "btnPause", btnPause);
        SetRef(speed, "btnSpeed1", speedButtons[0]);
        SetRef(speed, "btnSpeed2", speedButtons[1]);
        SetRef(speed, "btnSpeed3", speedButtons[2]);
    }

    // ==========================================
    // GIỮA TRÊN: VÒNG MÙA XOAY + NGÀY THÁNG
    // ==========================================
    private const float TopCenterWidth = 1128f;
    private const float ClockWidth = 250f;
    private const float TopBarHeight = 80f;

    private static void BuildSeasonClock(Transform parent)
    {
        RectTransform panel = CreatePanel("SeasonClock", parent, PanelColor);
        Place(panel, new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(-TopCenterWidth * 0.5f, -34f), new Vector2(ClockWidth, TopBarHeight));
        SeasonClockHUD clock = panel.gameObject.AddComponent<SeasonClockHUD>();

        RectTransform ringRoot = CreateRect("RingRoot", panel);
        Place(ringRoot, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(44f, -2f), new Vector2(60f, 60f));

        RectTransform ringRect = CreateRect("SeasonRing", ringRoot);
        Stretch(ringRect);
        SeasonRingGraphic ring = ringRect.gameObject.AddComponent<SeasonRingGraphic>();
        ring.raycastTarget = false;

        RectTransform dot = CreateRect("SeasonDot", ringRoot);
        Place(dot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24f, 24f));
        Image dotImage = AddImage(dot, Color.white, circleSprite, false);
        dotImage.raycastTarget = false;

        RectTransform pointer = CreateRect("Pointer", ringRoot);
        Place(pointer, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(11f, 11f));
        pointer.localRotation = Quaternion.Euler(0f, 0f, 45f);
        AddImage(pointer, TextColor, null).raycastTarget = false;

        TextMeshProUGUI txtDay = CreateText("Txt_DayAndSeason", panel, "Ngày 1, Mùa Xuân", 21f, TextColor, TextAlignmentOptions.Left);
        Place(txtDay.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(86f, 0f), new Vector2(158f, 30f));

        TextMeshProUGUI txtMonth = CreateText("Txt_MonthAndYear", panel, "Tháng 1 - Năm 1", 17f, SubTextColor, TextAlignmentOptions.Left);
        Place(txtMonth.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 1f), new Vector2(86f, -2f), new Vector2(158f, 26f));

        SetRef(clock, "ringPivot", ringRect);
        SetRef(clock, "ring", ring);
        SetRef(clock, "imgSeasonDot", dotImage);
        SetRef(clock, "txtDayAndSeason", txtDay);
        SetRef(clock, "txtMonthAndYear", txtMonth);
    }

    // ==========================================
    // GIỮA TRÊN: KHO TÀI NGUYÊN
    // ==========================================
    private static void BuildResources(Transform parent, TopBarHUDController topBar)
    {
        const float padding = 14f;
        const float itemWidth = 140f;
        float panelWidth = TopCenterWidth - ClockWidth - 10f;

        RectTransform panel = CreatePanel("ResourcesPanel", parent, PanelColor);
        Place(panel, new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(-TopCenterWidth * 0.5f + ClockWidth + 10f, -34f), new Vector2(panelWidth, TopBarHeight));

        (string label, string field)[] resources =
        {
            ("Gỗ", "txtWood"),
            ("Sắt", "txtIron"),
            ("Lương thực", "txtFood"),
            ("Than", "txtCoal"),
            ("Vàng", "txtGold"),
            ("Thuốc men", "txtMedicine")
        };

        for (int i = 0; i < resources.Length; i++)
        {
            float x = padding + i * itemWidth;

            if (i > 0)
            {
                RectTransform separator = CreateRect("Separator", panel);
                Place(separator, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(2f, 44f));
                AddImage(separator, SeparatorColor, null).raycastTarget = false;
            }

            RectTransform item = CreateRect($"Res_{resources[i].field.Substring(3)}", panel);
            Place(item, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(itemWidth, 56f));

            RectTransform icon = CreateRect("Icon", item);
            Place(icon, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(38f, 38f));
            AddImage(icon, IconColor, null).raycastTarget = false;

            TextMeshProUGUI name = CreateText("Txt_Name", item, resources[i].label, 15f, SubTextColor, TextAlignmentOptions.BottomLeft);
            Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(58f, 2f), new Vector2(itemWidth - 62f, 22f));

            TextMeshProUGUI value = CreateText("Txt_Value", item, "0", 24f, TextColor, TextAlignmentOptions.TopLeft);
            Place(value.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 1f), new Vector2(58f, 0f), new Vector2(itemWidth - 62f, 30f));

            SetRef(topBar, resources[i].field, value);
        }
    }

    // ==========================================
    // GÓC PHẢI TRÊN: THÔNG BÁO / THƯ / CÀI ĐẶT
    // ==========================================
    private static void BuildTopRightButtons(Transform parent)
    {
        string[] labels = { "Cài đặt", "Thư", "Thông báo" };
        for (int i = 0; i < labels.Length; i++)
        {
            float x = -34f - i * 74f;
            RectTransform btn = CreateIconButton($"Btn_{labels[i]}", parent, new Vector2(64f, 64f), 36f);
            Place(btn, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(x, -34f), new Vector2(64f, 64f));

            TextMeshProUGUI label = CreateText($"Label_{labels[i]}", parent, labels[i], 15f, TextColor, TextAlignmentOptions.Center);
            Place(label.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(x - 32f, -104f), new Vector2(90f, 22f));
        }
    }

    // ==========================================
    // TRÁI: TRẠNG THÁI THÀNH TRÌ
    // ==========================================
    private static void BuildCityStatus(Transform parent, TopBarHUDController topBar)
    {
        RectTransform panel = CreatePanel("CityStatusPanel", parent, PanelColor);
        Place(panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(52f, -136f), new Vector2(260f, 186f));

        RectTransform header = CreatePanel("Header", panel, HeaderColor);
        Place(header, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(260f, 44f));
        TextMeshProUGUI headerText = CreateText("Txt_Title", header, "Trạng thái thành trì", 19f, Color.white, TextAlignmentOptions.Center);
        Stretch(headerText.rectTransform);

        (string label, string field, Color color)[] rows =
        {
            ("Dân số", "txtPopulation", PositiveColor),
            ("Dịch bệnh", "txtInfection", NegativeColor),
            ("Ổn định", "txtHappiness", PositiveColor)
        };

        for (int i = 0; i < rows.Length; i++)
        {
            RectTransform row = CreateRect($"Row_{rows[i].field.Substring(3)}", panel);
            Place(row, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -56f - i * 42f), new Vector2(220f, 36f));

            RectTransform icon = CreateRect("Icon", row);
            Place(icon, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(24f, 24f));
            AddImage(icon, IconColor, null).raycastTarget = false;

            TextMeshProUGUI label = CreateText("Txt_Label", row, rows[i].label, 19f, TextColor, TextAlignmentOptions.Left);
            Place(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(36f, 0f), new Vector2(100f, 30f));

            TextMeshProUGUI value = CreateText("Txt_Value", row, "0", 19f, rows[i].color, TextAlignmentOptions.Left);
            Place(value.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(138f, 0f), new Vector2(80f, 30f));

            SetRef(topBar, rows[i].field, value);
        }
    }

    // ==========================================
    // TRÁI: THÔNG BÁO NHẮC NHỞ / CẢNH BÁO
    // ==========================================
    private static void BuildNotifications(Transform parent)
    {
        RectTransform group = CreateRect("Notifications", parent);
        Place(group, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(52f, -384f), new Vector2(400f, 140f));

        CreateNotification(group, "Notice_Reminder", "Lời nhắc: củi sắp hết", ReminderColor, new Vector2(0f, 0f), 310f);
        CreateNotification(group, "Notice_Warning", "Cảnh báo: dịch bệnh bùng phát", WarningColor, new Vector2(0f, -74f), 395f);
    }

    private static void CreateNotification(Transform parent, string name, string message, Color background, Vector2 position, float width)
    {
        RectTransform item = CreatePanel(name, parent, background);
        Place(item, new Vector2(0f, 1f), new Vector2(0f, 1f), position, new Vector2(width, 56f));

        RectTransform icon = CreateRect("Icon", item);
        Place(icon, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(24f, 24f));
        AddImage(icon, IconColor, null).raycastTarget = false;

        TextMeshProUGUI text = CreateText("Txt_Message", item, message, 17f, TextColor, TextAlignmentOptions.Left);
        Place(text.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(68f, 0f), new Vector2(width - 120f, 30f));

        RectTransform close = CreateRect("Btn_Close", item);
        Place(close, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(36f, 36f));
        Image closeImage = AddImage(close, new Color(1f, 1f, 1f, 0f), null);
        Button closeButton = close.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = closeImage;
        UnityEventTools.AddBoolPersistentListener(closeButton.onClick, new UnityAction<bool>(item.gameObject.SetActive), false);

        TextMeshProUGUI x = CreateText("Label", close, "X", 22f, TextColor, TextAlignmentOptions.Center);
        Stretch(x.rectTransform);
    }

    // ==========================================
    // PHẢI: CỐNG / NGHIÊN CỨU / LUẬT / THUẾ
    // ==========================================
    private static void BuildRightMenu(Transform parent)
    {
        string[] labels = { "Cống", "Nghiên cứu", "Luật", "Thuế" };
        for (int i = 0; i < labels.Length; i++)
        {
            float y = -204f - i * 72f;
            RectTransform btn = CreateIconButton($"Btn_{labels[i]}", parent, new Vector2(64f, 64f), 34f);
            Place(btn, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-46f, y), new Vector2(64f, 64f));

            TextMeshProUGUI label = CreateText($"Label_{labels[i]}", parent, labels[i], 15f, TextColor, TextAlignmentOptions.Right);
            Place(label.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-124f, y - 32f), new Vector2(160f, 26f));
        }
    }

    // ==========================================
    // DƯỚI GIỮA: THANH XÂY DỰNG
    // ==========================================
    private static void BuildBottomBar(Transform parent)
    {
        string[] labels = { "Xây dựng", "Nhà ở", "Sản xuất", "Nông nghiệp", "Hạ tầng", "Trang trí" };
        const float buttonSize = 84f;
        const float spacing = 8f;
        const float padding = 10f;
        float width = padding * 2f + labels.Length * buttonSize + (labels.Length - 1) * spacing;

        RectTransform panel = CreatePanel("BuildBar", parent, PanelColor);
        Place(panel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(width, buttonSize + padding * 2f));

        for (int i = 0; i < labels.Length; i++)
        {
            RectTransform btn = CreateIconButton($"Btn_{labels[i]}", panel, new Vector2(buttonSize, buttonSize), 34f);
            Place(btn, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(padding + i * (buttonSize + spacing), 0f), new Vector2(buttonSize, buttonSize));

            RectTransform icon = (RectTransform)btn.Find("Icon");
            icon.anchoredPosition = new Vector2(0f, 10f);

            TextMeshProUGUI label = CreateText("Label", btn, labels[i], 14f, TextColor, TextAlignmentOptions.Center);
            Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(buttonSize, 20f));
        }
    }

    // ==========================================
    // HELPERS
    // ==========================================
    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static RectTransform CreatePanel(string name, Transform parent, Color color)
    {
        RectTransform rt = CreateRect(name, parent);
        AddImage(rt, color, panelSprite);
        return rt;
    }

    private static RectTransform CreateIconButton(string name, Transform parent, Vector2 size, float iconSize)
    {
        RectTransform rt = CreatePanel(name, parent, ButtonColor);
        rt.sizeDelta = size;
        AddButton(rt);

        RectTransform icon = CreateRect("Icon", rt);
        Place(icon, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(iconSize, iconSize));
        AddImage(icon, IconColor, null).raycastTarget = false;
        return rt;
    }

    private static Button AddButton(RectTransform rt)
    {
        Button button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = rt.GetComponent<Image>();
        return button;
    }

    private static Image AddImage(RectTransform rt, Color color, Sprite sprite, bool sliced = true)
    {
        Image image = rt.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.type = sprite != null && sliced ? Image.Type.Sliced : Image.Type.Simple;
        image.color = color;
        return image;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions alignment)
    {
        RectTransform rt = CreateRect(name, parent);
        TextMeshProUGUI tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        return tmp;
    }

    private static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void SetRef(Object target, string field, Object value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty(field);
        if (property == null)
            return;

        property.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
