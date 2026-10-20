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

    private static readonly Color OverlayColor = new Color(0f, 0f, 0f, 0.45f);
    private static readonly Color PopupHeaderColor = new Color32(0x58, 0x68, 0x74, 0xFF);
    private static readonly Color PopupSectionColor = new Color32(0xDC, 0xE1, 0xE5, 0xFF);
    private static readonly Color PopupSectionHeaderColor = new Color32(0xC3, 0xCD, 0xD3, 0xFF);
    private static readonly Color FieldColor = new Color32(0xEF, 0xEF, 0xEF, 0xFF);
    private static readonly Color ChipColor = new Color32(0xC9, 0xC9, 0xC9, 0xFF);
    private static readonly Color ChipActiveColor = new Color32(0xAF, 0xAF, 0xAF, 0xFF);
    private static readonly Color CardColor = new Color32(0xC8, 0xC8, 0xC8, 0xFF);
    private static readonly Color DeleteColor = new Color32(0x9A, 0x9A, 0x9A, 0xFF);
    private static readonly Color ToggleOnColor = new Color32(0x7C, 0xB0, 0x7F, 0xFF);

    private static Sprite panelSprite;
    private static Sprite circleSprite;

    private static void LoadSprites()
    {
        panelSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        circleSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
    }

    [MenuItem("Tools/LordSimulation/Build Gameplay HUD")]
    public static void Build()
    {
        LoadSprites();

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

        EdictPopupController edictPopup = BuildEdictPopup(parent);
        WireButton<EdictPopupController>(parent, EdictButtonName, edictPopup.Show);

        PauseMenuController pauseMenu = BuildPauseMenu(parent);
        WireButton<PauseMenuController>(parent, PauseButtonName, pauseMenu.Show);

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private const string EdictPopupName = "EdictPopup";
    private const string EdictButtonName = "Btn_Luật";

    [MenuItem("Tools/LordSimulation/Add Edict Popup To Existing HUD")]
    public static void AddEdictPopupToExistingHUD()
    {
        LoadSprites();

        if (!TryOpenGameplayScene(out Scene scene))
            return;

        GameObject root = FindRoot(scene, RootName);
        if (root == null)
        {
            EditorUtility.DisplayDialog("Edict Popup", "Không tìm thấy GameplayHUD trong GamePlayScene. Hãy chạy Build Gameplay HUD trước.", "OK");
            return;
        }

        Transform old = root.transform.Find(EdictPopupName);
        if (old != null)
            Undo.DestroyObjectImmediate(old.gameObject);

        EdictPopupController popup = BuildEdictPopup(root.transform);
        Undo.RegisterCreatedObjectUndo(popup.gameObject, "Add Edict Popup");

        if (!WireButton<EdictPopupController>(root.transform, EdictButtonName, popup.Show))
            EditorUtility.DisplayDialog("Edict Popup", $"Không tìm thấy nút {EdictButtonName}. Popup đã được tạo, hãy tự gắn EdictPopupController.Show vào nút mở.", "OK");

        EnsureEventSystem(scene);

        Selection.activeGameObject = popup.gameObject;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private const string PauseMenuName = "PauseMenu";
    private const string PauseButtonName = "Btn_Cài đặt";

    [MenuItem("Tools/LordSimulation/Add Pause Menu To Existing HUD")]
    public static void AddPauseMenuToExistingHUD()
    {
        LoadSprites();

        if (!TryOpenGameplayScene(out Scene scene))
            return;

        GameObject root = FindRoot(scene, RootName);
        if (root == null)
        {
            EditorUtility.DisplayDialog("Pause Menu", "Không tìm thấy GameplayHUD trong GamePlayScene. Hãy chạy Build Gameplay HUD trước.", "OK");
            return;
        }

        Transform old = root.transform.Find(PauseMenuName);
        if (old != null)
            Undo.DestroyObjectImmediate(old.gameObject);

        PauseMenuController menu = BuildPauseMenu(root.transform);
        Undo.RegisterCreatedObjectUndo(menu.gameObject, "Add Pause Menu");

        if (!WireButton<PauseMenuController>(root.transform, PauseButtonName, menu.Show))
            EditorUtility.DisplayDialog("Pause Menu", $"Không tìm thấy nút {PauseButtonName}. Menu vẫn mở được bằng phím Esc.", "OK");

        EnsureEventSystem(scene);

        Selection.activeGameObject = menu.gameObject;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static bool WireButton<T>(Transform hudRoot, string buttonName, UnityAction action) where T : Object
    {
        Transform buttonTransform = hudRoot.Find(buttonName);
        Button button = buttonTransform != null ? buttonTransform.GetComponent<Button>() : null;
        if (button == null)
            return false;

        Undo.RecordObject(button, "Wire Popup Button");
        for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
        {
            Object target = button.onClick.GetPersistentTarget(i);
            if (target == null || target is T)
                UnityEventTools.RemovePersistentListener(button.onClick, i);
        }

        UnityEventTools.AddPersistentListener(button.onClick, action);
        EditorUtility.SetDirty(button);
        return true;
    }

    private static readonly string[] ResourceFields = { "txtWood", "txtIron", "txtFood", "txtCoal", "txtGold", "txtMedicine" };

    [MenuItem("Tools/LordSimulation/Add Resource Rates To Existing HUD")]
    public static void AddResourceRatesToExistingHUD()
    {
        if (!TryOpenGameplayScene(out Scene scene))
            return;

        TopBarHUDController topBar = FindInScene<TopBarHUDController>(scene);
        if (topBar == null)
        {
            EditorUtility.DisplayDialog("Resource Rates", "Không tìm thấy TopBarHUDController trong GamePlayScene.", "OK");
            return;
        }

        SerializedObject topBarSo = new SerializedObject(topBar);
        int added = 0;

        foreach (string field in ResourceFields)
        {
            TMP_Text value = topBarSo.FindProperty(field).objectReferenceValue as TMP_Text;
            SerializedProperty rateProperty = topBarSo.FindProperty(field + "Rate");
            if (value == null || rateProperty == null)
                continue;

            Transform oldRate = value.transform.parent.Find(RateObjectName);
            if (oldRate != null)
                Undo.DestroyObjectImmediate(oldRate.gameObject);

            TextMeshProUGUI rate = CreateRateText(value);
            Undo.RegisterCreatedObjectUndo(rate.gameObject, "Add Resource Rates");
            rateProperty.objectReferenceValue = rate;
            added++;
        }

        topBarSo.ApplyModifiedProperties();

        if (added == 0)
        {
            EditorUtility.DisplayDialog("Resource Rates", "TopBarHUDController chưa gắn ô tài nguyên nào.", "OK");
            return;
        }

        Selection.activeGameObject = topBar.gameObject;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private const string RateObjectName = "Txt_Rate";

    private static TextMeshProUGUI CreateRateText(TMP_Text value)
    {
        RectTransform valueRect = value.rectTransform;
        TextMeshProUGUI rate = CreateText(RateObjectName, valueRect.parent, "0/th", Mathf.Max(11f, value.fontSize * 0.58f), HeaderColor, TextAlignmentOptions.BottomRight);

        RectTransform rateRect = rate.rectTransform;
        rateRect.anchorMin = valueRect.anchorMin;
        rateRect.anchorMax = valueRect.anchorMax;
        rateRect.pivot = new Vector2(1f, valueRect.pivot.y);
        float rightEdge = valueRect.anchoredPosition.x + valueRect.sizeDelta.x * (1f - valueRect.pivot.x);
        rateRect.anchoredPosition = new Vector2(rightEdge, valueRect.anchoredPosition.y);
        rateRect.sizeDelta = new Vector2(Mathf.Min(64f, valueRect.sizeDelta.x), valueRect.sizeDelta.y);
        rateRect.SetSiblingIndex(valueRect.GetSiblingIndex() + 1);
        return rate;
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
            SetRef(topBar, resources[i].field + "Rate", CreateRateText(value));
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
    // POPUP: LUẬT (TẠO LUẬT + LUẬT ĐANG HOẠT ĐỘNG)
    // ==========================================
    private const float PopupWidth = 1280f;
    private const float PopupHeight = 620f;
    private const float PopupHeaderHeight = 88f;
    private const float SectionTop = 104f;
    private const float SectionHeight = 488f;
    private const float SectionHeaderHeight = 70f;
    private const float RuleRowHeight = 51f;

    private static EdictPopupController BuildEdictPopup(Transform parent)
    {
        RectTransform overlay = CreateRect(EdictPopupName, parent);
        Stretch(overlay);
        AddImage(overlay, OverlayColor, null);
        EdictPopupController popup = overlay.gameObject.AddComponent<EdictPopupController>();

        RectTransform window = CreateRect("Window", overlay);
        Place(window, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PopupWidth, PopupHeight));
        AddImage(window, Color.white, null);

        RectTransform header = CreateRect("Header", window);
        Place(header, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(PopupWidth, PopupHeaderHeight));
        AddImage(header, PopupHeaderColor, null);

        TextMeshProUGUI title = CreateText("Txt_Title", header, "Luật", 34f, Color.white, TextAlignmentOptions.Center);
        Stretch(title.rectTransform);

        RectTransform close = CreateRect("Btn_Close", header);
        Place(close, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-18f, 0f), new Vector2(56f, 56f));
        Image closeImage = AddImage(close, new Color(1f, 1f, 1f, 0f), null);
        Button btnClose = close.gameObject.AddComponent<Button>();
        btnClose.targetGraphic = closeImage;
        TextMeshProUGUI closeLabel = CreateText("Label", close, "X", 34f, Color.white, TextAlignmentOptions.Center);
        Stretch(closeLabel.rectTransform);

        BuildEdictCreateSection(window, popup);
        BuildEdictListSection(window, popup);

        SetRef(popup, "btnClose", btnClose);
        overlay.gameObject.SetActive(false);
        return popup;
    }

    private static void BuildEdictCreateSection(Transform window, EdictPopupController popup)
    {
        const float width = 698f;
        RectTransform section = CreateSection("CreateRulePanel", window, "Tạo luật mới", 32f, width);

        RectTransform content = CreateScrollContent("Body", section, new Vector2(0f, -SectionHeaderHeight), new Vector2(width, 310f), new RectOffset(0, 0, 26, 10), 40f);

        RectTransform conditions = CreateRect("Conditions", content);
        AddVerticalLayout(conditions, 14f);
        EdictConditionRowView firstRow = BuildConditionRow(conditions);

        RectTransform linkRow = CreateLayoutRow("LinkRow", content, RuleRowHeight);
        Button btnAnd = CreateChipButton("Btn_And", linkRow, "And", ChipActiveColor, 31f, 81f);
        Button btnOr = CreateChipButton("Btn_Or", linkRow, "Or", ChipColor, 112f, 80f);
        Button btnAdd = CreateChipButton("Btn_AddCondition", linkRow, "+ Thêm điều kiện", FieldColor, 216f, 449f, 16f);

        RectTransform thenRow = CreateLayoutRow("ThenRow", content, RuleRowHeight);
        RectTransform thenChip = CreatePanel("Chip_Then", thenRow, ChipColor);
        PlaceLeft(thenChip, 31f, 88f, RuleRowHeight);
        TextMeshProUGUI thenLabel = CreateText("Label", thenChip, "Then", 17f, TextColor, TextAlignmentOptions.Center);
        Stretch(thenLabel.rectTransform);

        TMP_Dropdown ddAction = CreateDropdown("Dd_Action", thenRow, 17f, 300f);
        PlaceLeft((RectTransform)ddAction.transform, 142f, 523f, RuleRowHeight);

        RectTransform activate = CreatePanel("Btn_Activate", section, FieldColor);
        Place(activate, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(31f, -395f), new Vector2(634f, 55f));
        Button btnActivate = AddButton(activate);
        TextMeshProUGUI activateLabel = CreateText("Label", activate, "Kích hoạt", 20f, TextColor, TextAlignmentOptions.Center);
        Stretch(activateLabel.rectTransform);

        TextMeshProUGUI status = CreateText("Txt_Status", section, string.Empty, 15f, SubTextColor, TextAlignmentOptions.Center);
        Place(status.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(31f, -454f), new Vector2(634f, 26f));

        SetRef(popup, "conditionContainer", conditions);
        SetRef(popup, "firstConditionRow", firstRow);
        SetRef(popup, "btnAnd", btnAnd);
        SetRef(popup, "btnOr", btnOr);
        SetRef(popup, "btnAddCondition", btnAdd);
        SetRef(popup, "ddAction", ddAction);
        SetRef(popup, "btnActivate", btnActivate);
        SetRef(popup, "txtStatus", status);
    }

    private static EdictConditionRowView BuildConditionRow(Transform parent)
    {
        RectTransform row = CreateLayoutRow("ConditionRow", parent, RuleRowHeight);
        EdictConditionRowView view = row.gameObject.AddComponent<EdictConditionRowView>();

        Button btnLink = CreateChipButton("Btn_Link", row, "If", ChipColor, 31f, 59f, 18f);
        ColorBlock colors = btnLink.colors;
        colors.disabledColor = Color.white;
        btnLink.colors = colors;
        TMP_Text linkLabel = btnLink.GetComponentInChildren<TMP_Text>();

        TMP_Dropdown ddVariable = CreateDropdown("Dd_Variable", row, 17f, 320f);
        PlaceLeft((RectTransform)ddVariable.transform, 114f, 272f, RuleRowHeight);

        TMP_Dropdown ddOperator = CreateDropdown("Dd_Operator", row, 20f, 240f);
        PlaceLeft((RectTransform)ddOperator.transform, 402f, 119f, RuleRowHeight);

        TMP_InputField input = CreateInputField("Input_Value", row, 17f);
        PlaceLeft((RectTransform)input.transform, 539f, 126f, RuleRowHeight);

        RectTransform remove = CreateRect("Btn_Remove", row);
        PlaceLeft(remove, 669f, 26f, 26f);
        Image removeImage = AddImage(remove, new Color(1f, 1f, 1f, 0f), null);
        Button btnRemove = remove.gameObject.AddComponent<Button>();
        btnRemove.targetGraphic = removeImage;
        TextMeshProUGUI removeLabel = CreateText("Label", remove, "X", 18f, SubTextColor, TextAlignmentOptions.Center);
        Stretch(removeLabel.rectTransform);

        SetRef(view, "btnLink", btnLink);
        SetRef(view, "txtLink", linkLabel);
        SetRef(view, "ddVariable", ddVariable);
        SetRef(view, "ddOperator", ddOperator);
        SetRef(view, "inputValue", input);
        SetRef(view, "btnRemove", btnRemove);
        return view;
    }

    private static void BuildEdictListSection(Transform window, EdictPopupController popup)
    {
        const float width = 493f;
        RectTransform section = CreateSection("ActiveRulesPanel", window, "Luật đang hoạt động", 755f, width);

        RectTransform content = CreateScrollContent("List", section, new Vector2(0f, -SectionHeaderHeight), new Vector2(width, SectionHeight - SectionHeaderHeight), new RectOffset(31, 31, 26, 18), 18f);
        EdictRuleCardView template = BuildRuleCard(content);

        TextMeshProUGUI empty = CreateText("Txt_Empty", section, "Chưa có luật nào được ban hành", 17f, SubTextColor, TextAlignmentOptions.Center);
        Place(empty.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(width - 62f, 30f));

        SetRef(popup, "ruleListContent", content);
        SetRef(popup, "ruleCardTemplate", template);
        SetRef(popup, "txtEmpty", empty);
    }

    private static EdictRuleCardView BuildRuleCard(Transform parent)
    {
        RectTransform card = CreatePanel("RuleCard_Template", parent, CardColor);
        LayoutElement element = card.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = 167f;
        EdictRuleCardView view = card.gameObject.AddComponent<EdictRuleCardView>();

        TextMeshProUGUI title = CreateText("Txt_Title", card, "Tăng sản xuất lương thực", 17f, TextColor, TextAlignmentOptions.Left);
        Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -14f), new Vector2(276f, 32f));
        title.overflowMode = TextOverflowModes.Ellipsis;

        RectTransform track = CreatePanel("Btn_Toggle", card, ToggleOnColor);
        Place(track, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(314f, -18f), new Vector2(45f, 25f));
        Image trackImage = track.GetComponent<Image>();
        trackImage.pixelsPerUnitMultiplier = 0.35f;
        Button btnToggle = AddButton(track);

        RectTransform knob = CreateRect("Knob", track);
        Place(knob, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-3f, 0f), new Vector2(19f, 19f));
        AddImage(knob, Color.white, circleSprite, false).raycastTarget = false;

        RectTransform delete = CreatePanel("Btn_Delete", card, DeleteColor);
        Place(delete, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(382f, -18f), new Vector2(25f, 25f));
        Button btnDelete = AddButton(delete);
        TextMeshProUGUI deleteLabel = CreateText("Label", delete, "X", 14f, Color.white, TextAlignmentOptions.Center);
        Stretch(deleteLabel.rectTransform);

        TextMeshProUGUI links = CreateCardColumn("Txt_Links", card, "If\nAnd\nThen", 24f, 52f);
        TextMeshProUGUI details = CreateCardColumn("Txt_Details", card, "Lương thực < 30\nDân số > 50\nTăng sản xuất nông nghiệp 20%", 79f, 328f);

        SetRef(view, "txtTitle", title);
        SetRef(view, "txtLinks", links);
        SetRef(view, "txtDetails", details);
        SetRef(view, "btnToggle", btnToggle);
        SetRef(view, "imgToggleTrack", trackImage);
        SetRef(view, "toggleKnob", knob);
        SetRef(view, "btnDelete", btnDelete);
        SetRef(view, "layoutElement", element);

        card.gameObject.SetActive(false);
        return view;
    }

    private static TextMeshProUGUI CreateCardColumn(string name, Transform parent, string text, float x, float width)
    {
        TextMeshProUGUI tmp = CreateText(name, parent, text, 16f, TextColor, TextAlignmentOptions.TopLeft);
        tmp.lineSpacing = 30f;
        tmp.richText = false;

        RectTransform rt = tmp.rectTransform;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.offsetMin = new Vector2(x, 10f);
        rt.offsetMax = new Vector2(x + width, -58f);
        return tmp;
    }

    private static RectTransform CreateSection(string name, Transform parent, string title, float x, float width)
    {
        RectTransform section = CreatePanel(name, parent, PopupSectionColor);
        Place(section, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -SectionTop), new Vector2(width, SectionHeight));

        RectTransform header = CreatePanel("Header", section, PopupSectionHeaderColor);
        Place(header, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(width, SectionHeaderHeight));
        TextMeshProUGUI text = CreateText("Txt_Title", header, title, 20f, TextColor, TextAlignmentOptions.Center);
        Stretch(text.rectTransform);
        return section;
    }

    private static RectTransform CreateScrollContent(string name, Transform parent, Vector2 position, Vector2 size, RectOffset padding, float spacing)
    {
        RectTransform viewport = CreateRect(name, parent);
        Place(viewport, new Vector2(0f, 1f), new Vector2(0f, 1f), position, size);
        AddImage(viewport, new Color(1f, 1f, 1f, 0f), null);
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform content = CreateRect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        VerticalLayoutGroup layout = AddVerticalLayout(content, spacing);
        layout.padding = padding;
        ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;
        return content;
    }

    private static VerticalLayoutGroup AddVerticalLayout(RectTransform rt, float spacing)
    {
        VerticalLayoutGroup layout = rt.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return layout;
    }

    private static RectTransform CreateLayoutRow(string name, Transform parent, float height)
    {
        RectTransform row = CreateRect(name, parent);
        LayoutElement element = row.gameObject.AddComponent<LayoutElement>();
        element.minHeight = height;
        element.preferredHeight = height;
        return row;
    }

    private static Button CreateChipButton(string name, Transform parent, string label, Color color, float x, float width, float fontSize = 17f)
    {
        RectTransform chip = CreatePanel(name, parent, color);
        PlaceLeft(chip, x, width, RuleRowHeight);
        Button button = AddButton(chip);
        TextMeshProUGUI text = CreateText("Label", chip, label, fontSize, TextColor, TextAlignmentOptions.Center);
        Stretch(text.rectTransform);
        return button;
    }

    private static TMP_DefaultControls.Resources DefaultControlResources()
    {
        return new TMP_DefaultControls.Resources
        {
            standard = panelSprite,
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
            knob = circleSprite,
            checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
            dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
            mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd")
        };
    }

    private static TMP_Dropdown CreateDropdown(string name, Transform parent, float fontSize, float listHeight)
    {
        GameObject go = TMP_DefaultControls.CreateDropdown(DefaultControlResources());
        go.name = name;
        go.transform.SetParent(parent, false);
        SetLayerRecursive(go.transform, LayerMask.NameToLayer("UI"));

        TMP_Dropdown dropdown = go.GetComponent<TMP_Dropdown>();
        dropdown.ClearOptions();
        go.GetComponent<Image>().color = FieldColor;

        RectTransform caption = dropdown.captionText.rectTransform;
        caption.offsetMin = new Vector2(18f, 4f);
        caption.offsetMax = new Vector2(-44f, -4f);
        StyleControlText(dropdown.captionText, fontSize);
        StyleControlText(dropdown.itemText, fontSize);

        RectTransform arrow = (RectTransform)go.transform.Find("Arrow");
        arrow.sizeDelta = new Vector2(18f, 18f);
        arrow.anchoredPosition = new Vector2(-24f, 0f);
        arrow.GetComponent<Image>().color = SubTextColor;

        RectTransform template = dropdown.template;
        template.sizeDelta = new Vector2(0f, listHeight);
        ((RectTransform)template.Find("Viewport/Content")).sizeDelta = new Vector2(0f, 40f);
        ((RectTransform)template.Find("Viewport/Content/Item")).sizeDelta = new Vector2(0f, 36f);
        dropdown.itemText.rectTransform.offsetMin = new Vector2(32f, 1f);
        return dropdown;
    }

    private static TMP_InputField CreateInputField(string name, Transform parent, float fontSize)
    {
        GameObject go = TMP_DefaultControls.CreateInputField(DefaultControlResources());
        go.name = name;
        go.transform.SetParent(parent, false);
        SetLayerRecursive(go.transform, LayerMask.NameToLayer("UI"));

        Image background = go.GetComponent<Image>();
        background.sprite = panelSprite;
        background.color = FieldColor;

        TMP_InputField input = go.GetComponent<TMP_InputField>();
        input.contentType = TMP_InputField.ContentType.DecimalNumber;
        input.text = "30";
        input.pointSize = fontSize;

        input.textComponent.alignment = TextAlignmentOptions.Center;
        input.textComponent.color = TextColor;

        if (input.placeholder is TMP_Text placeholder)
        {
            placeholder.text = "0";
            placeholder.fontStyle = FontStyles.Normal;
            placeholder.alignment = TextAlignmentOptions.Center;
        }
        return input;
    }

    private static void StyleControlText(TMP_Text text, float fontSize)
    {
        text.fontSize = fontSize;
        text.color = TextColor;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
    }

    private static void PlaceLeft(RectTransform rt, float x, float width, float height)
    {
        Place(rt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(width, height));
    }

    private static void SetLayerRecursive(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        foreach (Transform child in t)
            SetLayerRecursive(child, layer);
    }

    // ==========================================
    // POPUP: MENU TẠM DỪNG
    // ==========================================
    private static readonly Color PauseButtonColor = new Color32(0xDD, 0xE1, 0xE5, 0xFF);
    private static readonly Color PauseTextColor = new Color32(0x2E, 0x3A, 0x46, 0xFF);

    private static PauseMenuController BuildPauseMenu(Transform parent)
    {
        const float width = 482f;
        const float height = 680f;
        const float headerHeight = 87f;
        const float buttonX = 64f;
        const float buttonWidth = 354f;
        const float buttonHeight = 72f;
        const float firstButtonY = 111f;
        const float buttonStride = 88f;

        RectTransform menuRoot = CreateRect(PauseMenuName, parent);
        Stretch(menuRoot);
        PauseMenuController menu = menuRoot.gameObject.AddComponent<PauseMenuController>();

        RectTransform overlay = CreateRect("Overlay", menuRoot);
        Stretch(overlay);
        AddImage(overlay, OverlayColor, null);

        RectTransform window = CreateRect("Window", overlay);
        Place(window, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, height));
        AddImage(window, Color.white, null);

        RectTransform header = CreateRect("Header", window);
        Place(header, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(width, headerHeight));
        AddImage(header, PopupHeaderColor, null);

        TextMeshProUGUI title = CreateText("Txt_Title", header, "PAUSE", 32f, Color.white, TextAlignmentOptions.Center);
        Stretch(title.rectTransform);

        RectTransform close = CreateRect("Btn_Close", header);
        Place(close, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(52f, 52f));
        Image closeImage = AddImage(close, new Color(1f, 1f, 1f, 0f), null);
        Button btnClose = close.gameObject.AddComponent<Button>();
        btnClose.targetGraphic = closeImage;
        TextMeshProUGUI closeLabel = CreateText("Label", close, "X", 32f, Color.white, TextAlignmentOptions.Center);
        Stretch(closeLabel.rectTransform);

        (string label, string field)[] buttons =
        {
            ("RESUME", "btnResume"),
            ("SAVE GAME", "btnSave"),
            ("LOAD GAME", "btnLoad"),
            ("SETTINGS", "btnSettings"),
            ("MAIN MENU", "btnMainMenu"),
            ("EXIT", "btnExit")
        };

        for (int i = 0; i < buttons.Length; i++)
        {
            string objectName = "Btn_" + buttons[i].field.Substring(3);
            RectTransform btn = CreatePanel(objectName, window, PauseButtonColor);
            Place(btn, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(buttonX, -firstButtonY - i * buttonStride), new Vector2(buttonWidth, buttonHeight));
            Button button = AddButton(btn);
            TextMeshProUGUI label = CreateText("Label", btn, buttons[i].label, 20f, PauseTextColor, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
            SetRef(menu, buttons[i].field, button);
        }

        float statusY = firstButtonY + buttons.Length * buttonStride - 4f;
        TextMeshProUGUI status = CreateText("Txt_Status", window, string.Empty, 16f, SubTextColor, TextAlignmentOptions.Center);
        Place(status.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(buttonX, -statusY), new Vector2(buttonWidth, 30f));

        SetRef(menu, "root", overlay.gameObject);
        SetRef(menu, "btnClose", btnClose);
        SetRef(menu, "txtStatus", status);

        overlay.gameObject.SetActive(false);
        return menu;
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
