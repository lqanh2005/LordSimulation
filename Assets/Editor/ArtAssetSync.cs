using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Đồng bộ kích thước art isometric.
/// Chuẩn: 1 ô = 400x200 px  ->  PPU 400  ->  1 ô = 1 x 0.5 unit.
/// Pivot công trình = góc dưới cùng (góc trước) của footprint.
/// Footprint (x, y): x = số ô cạnh dưới-phải, y = số ô cạnh dưới-trái.
/// </summary>
public static class ArtAssetSync
{
    public const float PixelsPerUnit = 400f;
    public static readonly Vector2 TileSize = new Vector2(1f, 0.5f);

    private const string PrefabRoot = "Assets/Prefabs/Art";
    private const string TestScenePath = "Assets/Scenes/ArtTestMap.unity";

    /// <summary>Ô trống trong Farm.png dành cho ruộng (Rice/Drug/Dirt 4x6), tính từ origin của Farm.</summary>
    public static readonly Vector2Int FarmCropSlotOffset = new Vector2Int(2, 1);

    /// <summary>Prefab Farm gameplay thu nhỏ art theo hệ số này để cùng tỉ lệ với nhà (10x8 -> 5x4 ô).</summary>
    public const int FarmArtDownscale = 2;

    private struct Art
    {
        public string path;
        public Vector2Int footprint;
        public Vector2 pivot;
        public Vector2 spriteOffset; // offset của child Sprite so với góc footprint (prop nhỏ đặt giữa ô)
        public bool makePrefab;

        public Art(string path, int fx, int fy, float px, float py, bool makePrefab = true, float ox = 0f, float oy = 0f)
        {
            this.path = path;
            footprint = new Vector2Int(fx, fy);
            pivot = new Vector2(px, py);
            spriteOffset = new Vector2(ox, oy);
            this.makePrefab = makePrefab;
        }

        public string Name => Path.GetFileNameWithoutExtension(path);
        public string Folder => Path.GetFileName(Path.GetDirectoryName(path));
    }

    // Pivot X = vị trí góc dưới cùng (đo từ alpha của ảnh) / chiều rộng ảnh.
    private static readonly Art[] Arts =
    {
        // Map tiles: pivot tâm ô
        new Art("Assets/Texture/Map/GrassTile.png", 1, 1, 0.5f, 0.5f, false),
        new Art("Assets/Texture/Map/DirtTile.png", 1, 1, 0.5f, 0.5f, false),
        new Art("Assets/Texture/Map/GrassDecor.png", 1, 1, 0.5f, 0.3f, false),

        // Building
        new Art("Assets/Texture/Building/Campfire.png", 1, 1, 0.5f, 0.3f, true, 0f, 0.25f),
        new Art("Assets/Texture/Building/TemporaryTent.png", 1, 1, 0.5f, 0.02f),
        new Art("Assets/Texture/Building/WaterWell.png", 1, 1, 0.49f, 0f),
        new Art("Assets/Texture/Building/GuardTower.png", 1, 1, 0.49f, 0f),
        new Art("Assets/Texture/Building/Housing.png", 2, 2, 0.5f, 0f),
        new Art("Assets/Texture/Building/Hospital.png", 2, 2, 0.5f, 0f),
        new Art("Assets/Texture/Building/QuarantineStation.png", 2, 2, 0.5f, 0f),
        new Art("Assets/Texture/Building/Forge.png", 2, 2, 0.5f, 0f),
        new Art("Assets/Texture/Building/Housing_3x3.png", 3, 3, 0.49f, 0f),
        new Art("Assets/Texture/Building/CityHall.png", 3, 3, 0.5f, 0f),
        new Art("Assets/Texture/Building/ResearchCenter.png", 3, 3, 0.5f, 0f),
        new Art("Assets/Texture/Building/Fireplace.png", 3, 3, 0.5f, 0f),

        // Coal
        new Art("Assets/Texture/Coal/CoalCart.png", 1, 1, 0.46f, 0.25f, true, 0f, 0.25f),
        new Art("Assets/Texture/Coal/CoalStore.png", 2, 2, 528f / 956f, 0f),
        new Art("Assets/Texture/Coal/CoalCave.png", 4, 8, 1674f / 2466f, 0f),

        // Farm: khung 10x8, ruộng 4x6 đặt vào ô trống tại FarmCropSlotOffset
        new Art("Assets/Texture/Farm/Farm.png", 10, 8, 1602f / 3604f, 0f),
        new Art("Assets/Texture/Farm/Dirt_1.png", 4, 6, 1196f / 2001f, 0f),
        new Art("Assets/Texture/Farm/Dirt_2.png", 4, 6, 1204f / 2002f, 0f),
        new Art("Assets/Texture/Farm/Dirt_3.png", 4, 6, 1200f / 2004f, 0f),
        new Art("Assets/Texture/Farm/Rice_1.png", 4, 6, 1200f / 2002f, 0f),
        new Art("Assets/Texture/Farm/Rice_2.png", 4, 6, 1200f / 2002f, 0f),
        new Art("Assets/Texture/Farm/Rice_3.png", 4, 6, 1200f / 2004f, 0f),
        new Art("Assets/Texture/Farm/Rice_4.png", 4, 6, 1202f / 2003f, 0f),
        new Art("Assets/Texture/Farm/Drug_1.png", 4, 6, 1208f / 2018f, 0f),
        new Art("Assets/Texture/Farm/Drug_2.png", 4, 6, 1208f / 2020f, 0f),
        new Art("Assets/Texture/Farm/Drug_3.png", 4, 6, 1210f / 2033f, 0f),
        new Art("Assets/Texture/Farm/Drug_4.png", 4, 6, 1214f / 2046f, 0f),
    };

    [MenuItem("LordSim/Art/Sync All + Build Test Map")]
    public static void RunAll()
    {
        SyncImportSettings();
        CreatePrefabs();
        BuildTestScene();
    }

    [MenuItem("LordSim/Art/1. Sync Sprite Import Settings")]
    public static void SyncImportSettings()
    {
        int count = 0;
        foreach (Art art in Arts)
        {
            TextureImporter ti = AssetImporter.GetAtPath(art.path) as TextureImporter;
            if (ti == null)
            {
                Debug.LogWarning($"[ArtAssetSync] Không tìm thấy {art.path}");
                continue;
            }

            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = PixelsPerUnit;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.filterMode = FilterMode.Bilinear;

            TextureImporterSettings s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            s.spriteAlignment = (int)SpriteAlignment.Custom;
            s.spritePivot = art.pivot;
            ti.SetTextureSettings(s);
            ti.spritePivot = art.pivot;

            ti.SaveAndReimport();
            count++;
        }

        Debug.Log($"[ArtAssetSync] Đã đồng bộ {count} sprite: PPU {PixelsPerUnit}, pivot góc footprint.");
    }

    [MenuItem("LordSim/Art/2. Create Art Prefabs")]
    public static void CreatePrefabs()
    {
        int count = 0;
        foreach (Art art in Arts)
        {
            if (!art.makePrefab)
            {
                continue;
            }

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(art.path);
            if (sprite == null)
            {
                Debug.LogWarning($"[ArtAssetSync] Thiếu sprite {art.path}");
                continue;
            }

            string folder = $"{PrefabRoot}/{art.Folder}";
            EnsureFolder(folder);

            GameObject root = new GameObject(art.Name);
            root.AddComponent<IsoFootprint>().size = art.footprint;

            GameObject child = new GameObject("Sprite");
            child.transform.SetParent(root.transform, false);
            child.transform.localPosition = art.spriteOffset;
            child.AddComponent<SpriteRenderer>().sprite = sprite;

            PrefabUtility.SaveAsPrefabAsset(root, $"{folder}/{art.Name}.prefab");
            Object.DestroyImmediate(root);
            count++;
        }

        // Prefab ghép: khung Farm + ruộng đặt sẵn trong ô trống (dùng cho gameplay)
        count += CreateFarmComposite("Farm_Rice", "Assets/Texture/Farm/Rice_4.png") ? 1 : 0;
        count += CreateFarmComposite("Farm_Drug", "Assets/Texture/Farm/Drug_4.png") ? 1 : 0;
        // Farm gameplay: ruộng trống đổi theo mùa, click để trồng Lúa/Thuốc
        count += CreateFarmComposite("Farm_Field", "Assets/Texture/Farm/Dirt_1.png", true) ? 1 : 0;

        AttachMineCart();
        AddNightLights();

        AssetDatabase.SaveAssets();
        Debug.Log($"[ArtAssetSync] Đã tạo {count} prefab trong {PrefabRoot}");
    }

    // Xe goòng là một phần của Mỏ: đặt trên đường ray trước cửa hầm (vị trí tính theo tỉ lệ ảnh CoalCave).
    private static readonly Vector2 MineCartImageFraction = new Vector2(0.4663f, 0.733f);   // (x từ trái, y từ trên)
    private const float MineCartScale = 0.8f;

    [MenuItem("LordSim/Art/6. Attach Cart To Mine")]
    public static void AttachMineCart()
    {
        const string minePath = PrefabRoot + "/Coal/CoalCave.prefab";
        Sprite mine = LoadSprite("Assets/Texture/Coal/CoalCave.png");
        Sprite cart = LoadSprite("Assets/Texture/Coal/CoalCart.png");
        if (mine == null || cart == null || AssetDatabase.LoadAssetAtPath<GameObject>(minePath) == null)
        {
            Debug.LogWarning("[ArtAssetSync] Thiếu art Mỏ / Xe goòng");
            return;
        }

        // Toạ độ ảnh -> toạ độ cục bộ (gốc = pivot sprite Mỏ = góc footprint)
        Rect rect = mine.rect;
        float ppu = mine.pixelsPerUnit;
        Vector2 local = new Vector2(
            (MineCartImageFraction.x * rect.width - mine.pivot.x) / ppu,
            ((1f - MineCartImageFraction.y) * rect.height - mine.pivot.y) / ppu);

        GameObject root = PrefabUtility.LoadPrefabContents(minePath);
        Transform old = root.transform.Find("Cart");
        if (old != null)
            Object.DestroyImmediate(old.gameObject);

        GameObject cartGo = new GameObject("Cart");
        cartGo.transform.SetParent(root.transform, false);
        cartGo.transform.localPosition = local;
        cartGo.transform.localScale = Vector3.one * MineCartScale;
        SpriteRenderer sr = cartGo.AddComponent<SpriteRenderer>();
        sr.sprite = cart;
        sr.sortingOrder = 1;   // offset tương đối: vẽ trên Mỏ

        PrefabUtility.SaveAsPrefabAsset(root, minePath);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log($"[ArtAssetSync] Đã gắn xe goòng vào Mỏ tại {local}");
    }

    // Đèn đêm (Point Light 2D): prefab, vị trí so với gốc footprint (unit), bán kính ngoài, màu, cường độ đêm, độ lập loè.
    // Vị trí đo từ ảnh: x = (px - pivotX) / 400, y = (cao ảnh - py) / 400.
    private struct NightLightSpec
    {
        public string prefab;
        public Vector2 offset;
        public float radius;
        public Color color;
        public float nightIntensity;
        public float flicker;

        public NightLightSpec(string prefab, float x, float y, float radius, Color color, float nightIntensity, float flicker)
        {
            this.prefab = prefab;
            offset = new Vector2(x, y);
            this.radius = radius;
            this.color = color;
            this.nightIntensity = nightIntensity;
            this.flicker = flicker;
        }
    }

    private static readonly Color FireColor = new Color(1f, 0.62f, 0.28f);
    private static readonly Color FurnaceColor = new Color(1f, 0.52f, 0.2f);
    private static readonly Color WindowColor = new Color(1f, 0.82f, 0.5f);

    private static readonly NightLightSpec[] NightLights =
    {
        new NightLightSpec("Building/Campfire", 0f, 0.3f, 1.8f, FireColor, 1.6f, 0.12f),
        new NightLightSpec("Building/Fireplace", 0.07f, 1.3f, 2.6f, FurnaceColor, 1.5f, 0.08f),   // Xưởng lò: lò cao giữa
        new NightLightSpec("Building/Forge", 0.4f, 0.4f, 1.6f, FurnaceColor, 1.4f, 0.12f),        // Lò rèn: miệng lò
        new NightLightSpec("Building/Housing", 0.4f, 0.58f, 1.3f, WindowColor, 1.25f, 0.03f),      // Nhà dân: cửa sổ + cửa
        new NightLightSpec("Building/Housing_3x3", 0.53f, 0.84f, 1.7f, WindowColor, 1f, 0.03f),   // Nhà lớn: cửa + đèn lồng
        new NightLightSpec("Coal/CoalCave", -1.98f, 1.57f, 1.5f, WindowColor, 1.1f, 0.05f),        // Mỏ: đèn lồng cửa hầm
    };

    [MenuItem("LordSim/Art/5. Add Night Lights")]
    public static void AddNightLights()
    {
        int count = 0;
        foreach (NightLightSpec spec in NightLights)
        {
            string path = $"{PrefabRoot}/{spec.prefab}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                Debug.LogWarning($"[ArtAssetSync] Thiếu prefab {path}");
                continue;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            Transform old = root.transform.Find("NightLight");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            BuildingNightLight oldRootLight = root.GetComponent<BuildingNightLight>();
            if (oldRootLight != null)
                Object.DestroyImmediate(oldRootLight);   // Bản cũ gắn trên gốc

            GameObject lightGo = new GameObject("NightLight");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = spec.offset;

            var light = lightGo.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
            light.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Point;
            light.blendStyleIndex = 0;   // Cộng vào cùng kênh Multiply với Global Light
            light.color = spec.color;
            light.intensity = 1f;
            light.pointLightInnerRadius = spec.radius * 0.15f;
            light.pointLightOuterRadius = spec.radius;
            light.falloffIntensity = 0.6f;
            foreach (SortingLayer layer in SortingLayer.layers)
                light.AddTargetSortingLayer(layer.id);
            light.enabled = false;   // PlacementManager bật khi công trình đang hoạt động

            BuildingNightLight night = lightGo.AddComponent<BuildingNightLight>();
            SerializedObject so = new SerializedObject(night);
            so.FindProperty("lightSource").objectReferenceValue = light;
            so.FindProperty("nightIntensity").floatValue = spec.nightIntensity;
            so.FindProperty("dayIntensity").floatValue = spec.color == WindowColor ? 0f : 0.15f;   // Cửa sổ ban ngày không sáng
            so.FindProperty("flickerAmount").floatValue = spec.flicker;
            so.FindProperty("radiusFlicker").floatValue = spec.flicker * 0.5f;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
            count++;
        }

        Debug.Log($"[ArtAssetSync] Đã gắn đèn đêm cho {count} prefab.");
    }

    private static bool CreateFarmComposite(string prefabName, string cropPath, bool cropSwappable = false)
    {
        Sprite farm = LoadSprite("Assets/Texture/Farm/Farm.png");
        Sprite crop = LoadSprite(cropPath);
        if (farm == null || crop == null)
        {
            return false;
        }

        // Art Farm vẽ to gấp đôi so với các công trình khác (khung 10x8) -> thu nhỏ còn 5x4 ô
        GameObject root = new GameObject(prefabName);
        root.AddComponent<IsoFootprint>().size = new Vector2Int(10 / FarmArtDownscale, 8 / FarmArtDownscale);
        Vector3 scale = Vector3.one / FarmArtDownscale;

        GameObject farmGo = new GameObject("Sprite");
        farmGo.transform.SetParent(root.transform, false);
        farmGo.transform.localScale = scale;
        farmGo.AddComponent<SpriteRenderer>().sprite = farm;

        // Góc dưới của ô (ox, oy) so với góc dưới của ô (0, 0), theo lưới gốc của art rồi thu nhỏ
        Vector2Int o = FarmCropSlotOffset;
        GameObject cropGo = new GameObject("Crop");
        cropGo.transform.SetParent(root.transform, false);
        cropGo.transform.localPosition = new Vector3((o.x - o.y) * TileSize.x * 0.5f, (o.x + o.y) * TileSize.y * 0.5f, 0f) / FarmArtDownscale;
        cropGo.transform.localScale = scale;
        SpriteRenderer cropSr = cropGo.AddComponent<SpriteRenderer>();
        cropSr.sprite = crop;
        cropSr.sortingOrder = 1; // offset tương đối, vẽ trên khung Farm

        if (cropSwappable)
        {
            root.AddComponent<FarmCropView>().Configure(
                cropSr,
                LoadSprites("Farm/Dirt_1", "Farm/Dirt_2", "Farm/Dirt_3"),
                LoadSprites("Farm/Rice_1", "Farm/Rice_2", "Farm/Rice_3", "Farm/Rice_4"),
                LoadSprites("Farm/Drug_1", "Farm/Drug_2", "Farm/Drug_3", "Farm/Drug_4"));
        }

        EnsureFolder($"{PrefabRoot}/Farm");
        PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabRoot}/Farm/{prefabName}.prefab");
        Object.DestroyImmediate(root);
        return true;
    }

    // BuildingType gameplay -> prefab art. Type không có trong bảng giữ prefab cũ (ColorBlock).
    private static readonly (BuildingType type, string prefab)[] GameplayArt =
    {
        (BuildingType.Tent, "Building/TemporaryTent"),
        (BuildingType.House, "Building/Housing"),
        (BuildingType.LargeHouse, "Building/Housing_3x3"),
        (BuildingType.QuarantineWard, "Building/QuarantineStation"),
        (BuildingType.Forge, "Building/Forge"),
        (BuildingType.Farm, "Farm/Farm_Field"),
        (BuildingType.Mine, "Coal/CoalCave"),
        (BuildingType.Furnace, "Building/Fireplace"),
        (BuildingType.Clinic, "Building/Hospital"),
        (BuildingType.School, "Building/ResearchCenter"),
        (BuildingType.GuardPost, "Building/GuardTower"),
        (BuildingType.CityHall, "Building/CityHall"),
        (BuildingType.WaterWell, "Building/WaterWell"),
        (BuildingType.Campfire, "Building/Campfire"),
        (BuildingType.CoalStore, "Coal/CoalStore"),
        (BuildingType.CoalCart, "Coal/CoalCart"),
        (BuildingType.HerbFarm, "Farm/Farm_Drug"),
    };

    public static GameObject GetArtPrefab(BuildingType type)
    {
        foreach ((BuildingType t, string prefabPath) in GameplayArt)
        {
            if (t == type)
            {
                return AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/{prefabPath}.prefab");
            }
        }

        return null;
    }

    private const string GameplayScenePath = "Assets/Scenes/GamePlayScene.unity";

    [MenuItem("LordSim/Art/4. Hook Art Into Gameplay")]
    public static void HookIntoGameplay()
    {
        // Scene gameplay: lưới theo tile mới, map sinh lại
        var scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
        GridController grid = Object.FindFirstObjectByType<GridController>();
        if (grid != null)
        {
            SerializedObject so = new SerializedObject(grid);
            so.FindProperty("width").intValue = 30;
            so.FindProperty("height").intValue = 30;
            so.FindProperty("autoTileSize").boolValue = false;
            so.FindProperty("tileSize").vector2Value = TileSize;
            so.FindProperty("baseSprite").objectReferenceValue = LoadSprite("Assets/Texture/Map/GrassTile.png");

            SerializedProperty decors = so.FindProperty("decors");
            decors.ClearArray();
            AddDecor(decors, "Dirt", LoadSprite("Assets/Texture/Map/DirtTile.png"), DecorLayer.Ground, 0.06f,
                Vector2.zero, Vector2.one, false);
            AddDecor(decors, "GrassTuft", LoadSprite("Assets/Texture/Map/GrassDecor.png"), DecorLayer.Ground, 0.1f,
                new Vector2(0.15f, 0.05f), new Vector2(0.5f, 0.8f), true);
            so.ApplyModifiedPropertiesWithoutUndo();

            // MapData cũ trỏ tới sprite đã xoá -> xoá để sinh lại 30x30
            MapData mapData = grid.Data != null ? grid.Data : AssetDatabase.LoadAssetAtPath<MapData>("Assets/Resources/Map/MapData.asset");
            if (mapData != null)
            {
                mapData.Clear();
                EditorUtility.SetDirty(mapData);
            }

            grid.ClearGrid();
        }

        Camera cam = Camera.main;
        if (cam != null)
        {
            IsoTestCamera mover = cam.GetComponent<IsoTestCamera>();
            if (mover == null)
            {
                mover = cam.gameObject.AddComponent<IsoTestCamera>();
            }

            mover.rightDragPan = false;
            Vector3 center = grid != null ? grid.transform.position + new Vector3(0f, 7.5f, 0f) : Vector3.zero;
            cam.transform.position = new Vector3(center.x, center.y, cam.transform.position.z);
            cam.orthographicSize = 6f;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        // Database (art + footprint) và thanh xây dựng
        BuildMenuSetup.SetupAll();
        Debug.Log("[ArtAssetSync] Đã gắn art mới vào MainBuildingDatabase và GamePlayScene.");
    }

    [MenuItem("LordSim/Art/3. Build Art Test Map Scene")]
    public static void BuildTestScene()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Camera
        GameObject camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        Camera cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 8.5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.16f, 0.22f, 0.15f);
        camGo.transform.position = new Vector3(0f, 7.5f, -10f);
        camGo.AddComponent<IsoTestCamera>();

        // Grid
        GameObject gridGo = new GameObject("Grid");
        GridController grid = gridGo.AddComponent<GridController>();
        SerializedObject so = new SerializedObject(grid);
        so.FindProperty("width").intValue = 30;
        so.FindProperty("height").intValue = 30;
        so.FindProperty("autoTileSize").boolValue = false;
        so.FindProperty("tileSize").vector2Value = TileSize;
        so.FindProperty("baseSprite").objectReferenceValue = LoadSprite("Assets/Texture/Map/GrassTile.png");
        so.FindProperty("useRandomSeed").boolValue = false;
        so.FindProperty("seed").intValue = 20261112;
        so.FindProperty("saveWhenGenerated").boolValue = false;
        so.FindProperty("loadSavedMapOnStart").boolValue = false;

        SerializedProperty decors = so.FindProperty("decors");
        decors.ClearArray();
        AddDecor(decors, "Dirt", LoadSprite("Assets/Texture/Map/DirtTile.png"), DecorLayer.Ground, 0.08f,
            Vector2.zero, new Vector2(1f, 1f), false);
        AddDecor(decors, "GrassTuft", LoadSprite("Assets/Texture/Map/GrassDecor.png"), DecorLayer.Object, 0.12f,
            new Vector2(0.15f, 0.05f), new Vector2(0.5f, 0.8f), true);
        so.ApplyModifiedPropertiesWithoutUndo();

        // Layout
        GameObject layoutGo = new GameObject("ArtLayout");
        IsoTestLayout layout = layoutGo.AddComponent<IsoTestLayout>();
        layout.grid = grid;
        layout.entries = BuildEntries();

        grid.GenerateNewMap();
        layout.Build();

        EnsureFolder("Assets/Scenes");
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), TestScenePath);
        Debug.Log($"[ArtAssetSync] Đã tạo scene test {TestScenePath} ({layout.entries.Count} công trình)");
    }

    private static List<IsoTestLayout.Entry> BuildEntries()
    {
        List<IsoTestLayout.Entry> list = new List<IsoTestLayout.Entry>();

        void Add(string folder, string name, int x, int y, bool overlay = false)
        {
            IsoFootprint prefab = AssetDatabase.LoadAssetAtPath<IsoFootprint>($"{PrefabRoot}/{folder}/{name}.prefab");
            if (prefab == null)
            {
                Debug.LogWarning($"[ArtAssetSync] Thiếu prefab {folder}/{name}");
                return;
            }

            list.Add(new IsoTestLayout.Entry { prefab = prefab, origin = new Vector2Int(x, y), overlay = overlay });
        }

        void AddFarm(int x, int y, string crop)
        {
            Add("Farm", "Farm", x, y);
            Add("Farm", crop, x + FarmCropSlotOffset.x, y + FarmCropSlotOffset.y, true);
        }

        // Khu nông nghiệp (phía trước-trái)
        AddFarm(1, 1, "Rice_4");
        AddFarm(1, 11, "Drug_4");
        Add("Farm", "Dirt_1", 1, 21);
        Add("Farm", "Rice_2", 6, 21);

        // Khu mỏ than (phía trước-phải)
        Add("Coal", "CoalCave", 14, 1);
        Add("Coal", "CoalStore", 19, 1);
        Add("Coal", "CoalCart", 19, 4);
        Add("Coal", "CoalCart", 20, 5);

        // Trung tâm
        Add("Building", "CityHall", 15, 14);
        Add("Building", "ResearchCenter", 20, 14);
        Add("Building", "Fireplace", 15, 19);
        Add("Building", "Hospital", 20, 19);
        Add("Building", "Housing", 24, 14);
        Add("Building", "Housing", 24, 17);
        Add("Building", "Housing", 24, 20);
        Add("Building", "WaterWell", 19, 11);
        Add("Building", "TemporaryTent", 14, 23);
        Add("Building", "TemporaryTent", 16, 23);
        Add("Building", "TemporaryTent", 18, 23);
        Add("Building", "Campfire", 16, 25);

        // Tháp canh 4 góc
        Add("Building", "GuardTower", 13, 12);
        Add("Building", "GuardTower", 28, 12);
        Add("Building", "GuardTower", 13, 27);
        Add("Building", "GuardTower", 28, 27);

        return list;
    }

    private static void AddDecor(SerializedProperty list, string id, Sprite sprite, DecorLayer layer, float chance,
        Vector2 randomOffset, Vector2 scaleRange, bool flip)
    {
        int i = list.arraySize;
        list.InsertArrayElementAtIndex(i);
        SerializedProperty e = list.GetArrayElementAtIndex(i);
        e.FindPropertyRelative("id").stringValue = id;
        e.FindPropertyRelative("sprite").objectReferenceValue = sprite;
        e.FindPropertyRelative("layer").enumValueIndex = (int)layer;
        e.FindPropertyRelative("chance").floatValue = chance;
        e.FindPropertyRelative("offset").vector2Value = Vector2.zero;
        e.FindPropertyRelative("randomOffset").vector2Value = randomOffset;
        e.FindPropertyRelative("scaleRange").vector2Value = scaleRange;
        e.FindPropertyRelative("randomFlipX").boolValue = flip;
    }

    private static Sprite LoadSprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

    private static Sprite[] LoadSprites(params string[] texturePaths)
    {
        Sprite[] sprites = new Sprite[texturePaths.Length];
        for (int i = 0; i < texturePaths.Length; i++)
        {
            sprites[i] = LoadSprite($"Assets/Texture/{texturePaths[i]}.png");
        }

        return sprites;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
