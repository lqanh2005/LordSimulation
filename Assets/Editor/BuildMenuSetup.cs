using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class BuildMenuSetup
{
    private const string GameplayScenePath = "Assets/Scenes/GamePlayScene.unity";
    private const string DatabasePath = "Assets/Data/MainBuildingDatabase.asset";
    private const string TemplatePrefabPath = "Assets/Sprites/House_ColorBlock.prefab";
    private const string GeneratedPrefabFolder = "Assets/Prefabs/Building";
    private const string HudRootName = "GameplayHUD";
    private const string BuildBarName = "BuildBar";
    private const string OptionsPanelName = "BuildOptions";

    private static readonly Color PanelColor = new Color32(0xE2, 0xE5, 0xE7, 0xFF);
    private static readonly Color OptionColor = new Color32(0xD3, 0xD7, 0xDA, 0xFF);
    private static readonly Color TextColor = new Color32(0x33, 0x33, 0x33, 0xFF);
    private static readonly Color SubTextColor = new Color32(0x6B, 0x6F, 0x73, 0xFF);

    private struct BuildingSpec
    {
        public BuildingType type;
        public string displayName;
        public int size;
        public int wood;
        public int gold;
        public Color color;

        public BuildingSpec(BuildingType type, string displayName, int size, int wood, int gold, Color color)
        {
            this.type = type;
            this.displayName = displayName;
            this.size = size;
            this.wood = wood;
            this.gold = gold;
            this.color = color;
        }
    }

    private static readonly BuildingSpec[] Specs =
    {
        new BuildingSpec(BuildingType.Tent, "Lều tạm", 1, 10, 0, new Color(0.85f, 0.8f, 0.65f)),
        new BuildingSpec(BuildingType.House, "Nhà dân", 1, 30, 20, new Color(0f, 0.14f, 0.73f)),
        new BuildingSpec(BuildingType.LargeHouse, "Nhà lớn", 3, 70, 50, new Color(0.2f, 0.35f, 0.8f)),
        new BuildingSpec(BuildingType.LumberCamp, "Trại gỗ", 2, 15, 10, new Color(0.45f, 0.3f, 0.15f)),
        new BuildingSpec(BuildingType.Farm, "Nông trại", 2, 25, 20, new Color(0.27f, 0.02f, 0.04f)),
        new BuildingSpec(BuildingType.Mine, "Mỏ", 2, 40, 40, new Color(0.25f, 0.25f, 0.35f)),
        new BuildingSpec(BuildingType.Furnace, "Xưởng lò", 3, 40, 60, new Color(0.34f, 0.34f, 0.34f)),
        new BuildingSpec(BuildingType.Forge, "Lò rèn", 2, 45, 50, new Color(0.7f, 0.35f, 0.25f)),
        new BuildingSpec(BuildingType.Clinic, "Phòng khám", 2, 50, 80, new Color(0.9f, 0.4f, 0.4f)),
        new BuildingSpec(BuildingType.School, "Trường học", 2, 50, 60, new Color(0.9f, 0.75f, 0.2f)),
        new BuildingSpec(BuildingType.GuardPost, "Trạm gác", 1, 30, 40, new Color(0.15f, 0.4f, 0.2f)),
        new BuildingSpec(BuildingType.QuarantineWard, "Khu cách ly", 2, 40, 50, new Color(0.55f, 0.3f, 0.6f)),
        new BuildingSpec(BuildingType.CityHall, "Tòa thị chính", 3, 80, 100, new Color(0.9f, 0.8f, 0.6f)),
        new BuildingSpec(BuildingType.WaterWell, "Giếng nước", 1, 15, 5, new Color(0.4f, 0.6f, 0.9f)),
        new BuildingSpec(BuildingType.CoalStore, "Kho than", 2, 30, 20, new Color(0.3f, 0.3f, 0.3f)),
        new BuildingSpec(BuildingType.Campfire, "Lửa trại", 1, 5, 0, new Color(0.95f, 0.5f, 0.1f)),
        new BuildingSpec(BuildingType.CoalCart, "Xe goòng", 1, 5, 5, new Color(0.6f, 0.45f, 0.2f)),
        new BuildingSpec(BuildingType.HerbFarm, "Vườn thuốc", 4, 25, 30, new Color(0.4f, 0.7f, 0.5f))
    };

    private static readonly (string label, BuildingType[] types)[] Categories =
    {
        ("Xây dựng", new[]
        {
            BuildingType.Tent, BuildingType.House, BuildingType.LargeHouse, BuildingType.LumberCamp, BuildingType.Farm,
            BuildingType.Mine, BuildingType.CoalStore, BuildingType.Furnace, BuildingType.Forge, BuildingType.CityHall, BuildingType.Clinic,
            BuildingType.School, BuildingType.GuardPost, BuildingType.QuarantineWard, BuildingType.WaterWell,
            BuildingType.Campfire, BuildingType.CoalCart
        }),
        ("Nhà ở", new[] { BuildingType.Tent, BuildingType.House, BuildingType.LargeHouse }),
        ("Sản xuất", new[] { BuildingType.LumberCamp, BuildingType.Mine, BuildingType.CoalStore, BuildingType.Furnace, BuildingType.Forge }),
        // Vườn thuốc gộp vào Farm (click Farm -> trồng thuốc); HerbFarm giữ lại cho save cũ
        ("Nông nghiệp", new[] { BuildingType.Farm }),
        ("Hạ tầng", new[]
        {
            BuildingType.CityHall, BuildingType.Clinic, BuildingType.School, BuildingType.GuardPost,
            BuildingType.QuarantineWard, BuildingType.WaterWell, BuildingType.Campfire
        }),
        ("Trang trí", new[] { BuildingType.CoalCart })
    };

    private static Sprite panelSprite;

    [MenuItem("Tools/LordSimulation/Setup Build Menu")]
    public static void SetupAll()
    {
        panelSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        BuildingDatabase database = AssetDatabase.LoadAssetAtPath<BuildingDatabase>(DatabasePath);
        if (database == null)
        {
            EditorUtility.DisplayDialog("Build Menu", $"Không tìm thấy {DatabasePath}.", "OK");
            return;
        }

        FillDatabase(database);

        if (!TryOpenScene(GameplayScenePath, out Scene scene))
            return;

        if (!BuildMenu(scene))
            return;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[BuildMenuSetup] Đã cập nhật database và thanh xây dựng.");
    }

    [MenuItem("Tools/LordSimulation/Fill Building Database")]
    public static void FillDatabaseMenu()
    {
        BuildingDatabase database = AssetDatabase.LoadAssetAtPath<BuildingDatabase>(DatabasePath);
        if (database != null)
            FillDatabase(database);
    }

    private static void FillDatabase(BuildingDatabase database)
    {
        if (database.allConfigs == null)
            database.allConfigs = new List<BuildingConfig>();

        foreach (BuildingSpec spec in Specs)
        {
            BuildingConfig config = database.allConfigs.Find(c => c.type == spec.type);
            if (config == null)
            {
                config = new BuildingConfig { type = spec.type };
                database.allConfigs.Add(config);
            }

            config.displayName = spec.displayName;
            config.costWood = spec.wood;
            config.costGold = spec.gold;

            // Ưu tiên art isometric; chỉ tạo khối màu khi chưa có art
            GameObject art = ArtAssetSync.GetArtPrefab(spec.type);
            if (art != null)
                config.visualPrefab = art;
            else if (config.visualPrefab == null)
                config.visualPrefab = EnsurePlaceholderPrefab(spec);

            // Footprint thật nằm ở IsoFootprint của prefab -> không ghi đè bằng spec
            IsoFootprint footprint = config.visualPrefab != null ? config.visualPrefab.GetComponent<IsoFootprint>() : null;
            config.size = footprint != null ? Mathf.Max(footprint.size.x, footprint.size.y) : spec.size;
        }

        database.allConfigs.Sort((a, b) => System.Array.FindIndex(Specs, s => s.type == a.type)
            .CompareTo(System.Array.FindIndex(Specs, s => s.type == b.type)));

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
    }

    private static GameObject EnsurePlaceholderPrefab(BuildingSpec spec)
    {
        string path = $"{GeneratedPrefabFolder}/{spec.type}_ColorBlock.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
            return existing;

        if (!AssetDatabase.IsValidFolder(GeneratedPrefabFolder))
            AssetDatabase.CreateFolder("Assets/Prefabs", "Building");

        if (!AssetDatabase.CopyAsset(TemplatePrefabPath, path))
            return null;

        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        contents.name = $"{spec.type}_ColorBlock";
        contents.transform.localScale = new Vector3(spec.size, spec.size * 0.5f, 1f);
        SpriteRenderer renderer = contents.GetComponent<SpriteRenderer>();
        if (renderer != null)
            renderer.color = spec.color;
        PrefabUtility.SaveAsPrefabAsset(contents, path);
        PrefabUtility.UnloadPrefabContents(contents);

        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    private static bool BuildMenu(Scene scene)
    {
        GameObject hud = FindRoot(scene, HudRootName);
        Transform buildBar = hud != null ? hud.transform.Find(BuildBarName) : null;
        if (buildBar == null)
        {
            EditorUtility.DisplayDialog("Build Menu", "Không tìm thấy GameplayHUD/BuildBar. Hãy chạy Build Gameplay HUD trước.", "OK");
            return false;
        }

        PlacementManager placement = FindInScene<PlacementManager>(scene);
        if (placement != null)
        {
            SerializedObject placementSo = new SerializedObject(placement);
            placementSo.FindProperty("isPlacing").boolValue = false;
            placementSo.ApplyModifiedPropertiesWithoutUndo();
        }

        Transform oldPanel = hud.transform.Find(OptionsPanelName);
        if (oldPanel != null)
            Object.DestroyImmediate(oldPanel.gameObject);

        BuildMenuController controller = buildBar.GetComponent<BuildMenuController>();
        if (controller == null)
            controller = buildBar.gameObject.AddComponent<BuildMenuController>();

        RectTransform panel = CreateRect(OptionsPanelName, hud.transform);
        panel.anchorMin = new Vector2(0.5f, 0f);
        panel.anchorMax = new Vector2(0.5f, 0f);
        panel.pivot = new Vector2(0.5f, 0f);
        panel.anchoredPosition = new Vector2(0f, 142f);
        AddImage(panel, PanelColor, panelSprite);

        // Lưới 8 cột để danh mục nhiều công trình không tràn màn hình
        GridLayoutGroup layout = panel.gameObject.AddComponent<GridLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.spacing = new Vector2(8f, 8f);
        layout.cellSize = new Vector2(170f, 96f);
        layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        layout.childAlignment = TextAnchor.LowerCenter;
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 8;

        ContentSizeFitter fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        BuildOptionView template = CreateOptionTemplate(panel);

        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("placementManager").objectReferenceValue = placement;
        so.FindProperty("optionsPanel").objectReferenceValue = panel;
        so.FindProperty("optionsContent").objectReferenceValue = panel;
        so.FindProperty("optionTemplate").objectReferenceValue = template;

        SerializedProperty categories = so.FindProperty("categories");
        categories.arraySize = Categories.Length;
        for (int i = 0; i < Categories.Length; i++)
        {
            SerializedProperty element = categories.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("name").stringValue = Categories[i].label;

            Transform buttonTransform = buildBar.Find("Btn_" + Categories[i].label);
            element.FindPropertyRelative("button").objectReferenceValue =
                buttonTransform != null ? buttonTransform.GetComponent<Button>() : null;

            SerializedProperty types = element.FindPropertyRelative("types");
            types.arraySize = Categories[i].types.Length;
            for (int t = 0; t < Categories[i].types.Length; t++)
                types.GetArrayElementAtIndex(t).intValue = (int)Categories[i].types[t];
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        panel.gameObject.SetActive(false);
        return true;
    }

    private static BuildOptionView CreateOptionTemplate(Transform parent)
    {
        RectTransform option = CreateRect("Option_Template", parent);
        option.sizeDelta = new Vector2(170f, 96f);
        Image image = AddImage(option, OptionColor, panelSprite);
        Button button = option.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        TextMeshProUGUI name = CreateText("Txt_Name", option, "Tên công trình", 18f, TextColor);
        name.fontStyle = FontStyles.Bold;
        Place(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(160f, 32f));

        TextMeshProUGUI cost = CreateText("Txt_Cost", option, "Gỗ 0 · Vàng 0", 14f, SubTextColor);
        Place(cost.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(160f, 26f));

        BuildOptionView view = option.gameObject.AddComponent<BuildOptionView>();
        SerializedObject so = new SerializedObject(view);
        so.FindProperty("button").objectReferenceValue = button;
        so.FindProperty("txtName").objectReferenceValue = name;
        so.FindProperty("txtCost").objectReferenceValue = cost;
        so.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    private static bool TryOpenScene(string path, out Scene scene)
    {
        scene = SceneManager.GetSceneByPath(path);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return false;
            scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }

        if (!scene.IsValid())
            return false;

        SceneManager.SetActiveScene(scene);
        return true;
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

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == name)
                return root;
        }
        return null;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static Image AddImage(RectTransform rt, Color color, Sprite sprite)
    {
        Image image = rt.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
        image.color = color;
        return image;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string text, float size, Color color)
    {
        RectTransform rt = CreateRect(name, parent);
        TextMeshProUGUI tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        return tmp;
    }

    private static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, anchor.y);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }
}
