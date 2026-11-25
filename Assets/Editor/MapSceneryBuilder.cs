using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Dựng cảnh quan quanh thành (nền đất, tường, cột, cổng, núi, cây, phế tích) trong GamePlayScene
/// theo Decor/map_Mockup.jpg. Bố cục ở Assets/Editor/MapSceneryLayout.txt — chạy lại an toàn (xoá "MapScenery" cũ rồi dựng lại).
/// </summary>
public static class MapSceneryBuilder
{
    private const string ScenePath = "Assets/Scenes/GamePlayScene.unity";
    private const string LayoutPath = "Assets/Editor/MapSceneryLayout.txt";
    private const string MapDataPath = "Assets/Resources/Map/MapData.asset";
    private const string MapFolder = "Assets/Texture/Map";
    private const string RootName = "MapScenery";
    private const string GateName = "CityGate";
    private const string DirtId = "Dirt";

    private const int GridSize = 30;
    private const float PixelsPerUnit = 400f;

    // Quy đổi pixel mockup -> world: 1 unit = 28.2 px, tâm thành (636, 321) ứng với (0, 7.25)
    private const float MockupPpu = 28.2f;
    private static readonly Vector2 MockupCenter = new Vector2(636f, 321f);
    private static readonly Rect BasePx = new Rect(14f, -6f, 1252f, 681f);

    // Thứ tự vẽ: nền -50 < đường -40.. < (mesh nền 0, decor 1, đường 2) < sau thành 10.. < tường sau 80..
    // < mọi thứ trong thành (100..218) < tường trước 230.. < trước thành 300..
    private const int BaseOrder = -50, FlatOrder = -40, BackOrder = 10, BackWallOrder = 80, FrontWallOrder = 230, FrontOrder = 300;

    private const int SegmentCells = 2;       // mỗi khúc tường dài 2 ô
    private const int GateSegment = 8;        // khúc thứ 8 ở cạnh dưới-phải là cổng
    private const float WallScale = 0.72f;
    private const float WallDrop = 0.42f;     // chân sprite tường thấp hơn mép ô
    private static readonly Vector2Int GateCell = new Vector2Int(16, -1);
    private static readonly Color ForegroundTint = new Color(0.45f, 0.49f, 0.41f);

    private static Dictionary<string, Sprite> _sprites;

    [MenuItem("LordSim/Art/7. Build Map Scenery")]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        ConfigureSprites();
        var scene = EditorSceneManager.OpenScene(ScenePath);
        foreach (GameObject go in scene.GetRootGameObjects())
            if (go.name == RootName)
                Object.DestroyImmediate(go);

        Transform root = new GameObject(RootName).transform;
        BuildBase(root);
        BuildWalls(root);
        BuildGate(root);
        List<Vector2Int> dirt = BuildLayout(root);
        UpdateMapData(dirt);
        ConfigureCamera();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[MapSceneryBuilder] Đã dựng cảnh quan theo mockup.");
    }

    private static string Nfc(string s) => s.Normalize(NormalizationForm.FormC);

    private static void ConfigureSprites()
    {
        _sprites = new Dictionary<string, Sprite>();
        // Tile nền cũ do ArtAssetSync quản lý
        var skip = new HashSet<string> { "GrassTile", "DirtTile", "GrassDecor", "map_Mockup" };
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { MapFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string key = Nfc(Path.GetFileNameWithoutExtension(path));
            if (skip.Contains(key) || !(AssetImporter.GetAtPath(path) is TextureImporter ti))
                continue;

            int maxSize = TextureSizeOptimizer.GetMaxSize(path);
            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            bool ready = ti.textureType == TextureImporterType.Sprite && ti.spriteImportMode == SpriteImportMode.Single
                && ti.mipmapEnabled && ti.maxTextureSize == maxSize
                && s.spriteAlignment == (int)SpriteAlignment.BottomCenter;
            if (ready) // đã đúng thì khỏi import lại (ảnh gốc rất lớn, import lâu)
            {
                _sprites[key] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                continue;
            }

            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            // Ảnh đã cấu hình có thể dùng PPU riêng (file gốc đã thu nhỏ) -> chỉ đặt cho ảnh mới
            if (ti.spritePixelsPerUnit == 100f)
                ti.spritePixelsPerUnit = PixelsPerUnit;
            ti.alphaIsTransparency = true;
            ti.filterMode = FilterMode.Bilinear;
            ti.mipmapEnabled = true; // ảnh rất lớn, hiển thị nhỏ -> mipmap chống nhiễu răng cưa
            ti.maxTextureSize = maxSize;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;

            ti.ReadTextureSettings(s);
            s.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            s.spriteMeshType = SpriteMeshType.Tight;
            ti.SetTextureSettings(s);
            ti.SaveAndReimport();

            _sprites[key] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }

    private static Vector2 FromMockup(float px, float py) =>
        new Vector2((px - MockupCenter.x) / MockupPpu, 7.25f - (py - MockupCenter.y) / MockupPpu);

    private static Vector2 FromGrid(float gx, float gy) => new Vector2((gx - gy) * 0.5f, (gx + gy) * 0.25f);

    private static Transform Group(Transform root, string name)
    {
        Transform t = new GameObject(name).transform;
        t.SetParent(root, false);
        return t;
    }

    /// <summary>Đặt sprite với chân ở <paramref name="pos"/>; height theo unit (giữ tỉ lệ ảnh).</summary>
    private static SpriteRenderer Put(Transform parent, string key, Vector2 pos, float height, int order)
    {
        key = Nfc(key);
        if (!_sprites.TryGetValue(key, out Sprite sprite) || sprite == null)
        {
            Debug.LogWarning($"[MapSceneryBuilder] Thiếu sprite '{key}'");
            return null;
        }

        var go = new GameObject(key);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        float scale = height / (sprite.rect.height / sprite.pixelsPerUnit);
        go.transform.localScale = new Vector3(scale, scale, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        return sr;
    }

    private static void BuildBase(Transform root)
    {
        SpriteRenderer sr = Put(root, "BaseDirt", FromMockup(BasePx.center.x, BasePx.yMax), BasePx.height / MockupPpu, BaseOrder);
        Vector3 s = sr.transform.localScale;
        s.x = BasePx.width / MockupPpu / (sr.sprite.rect.width / sr.sprite.pixelsPerUnit);
        sr.transform.localScale = s;
    }

    private static void Wall(Transform parent, string key, float gx, float gy, float drop, int order)
    {
        Sprite sprite = _sprites[Nfc(key)];
        Put(parent, key, FromGrid(gx, gy) + Vector2.down * drop, sprite.rect.height / sprite.pixelsPerUnit * WallScale, order);
    }

    private static void BuildWalls(Transform root)
    {
        Transform parent = Group(root, "Walls");
        const float min = -0.5f, max = GridSize - 0.5f, cornerDrop = 0.28f;
        int count = GridSize / SegmentCells;

        // i lớn = xa hơn -> vẽ trước
        for (int i = count - 1; i >= 0; i--)
        {
            float c = min + SegmentCells * (i + 0.5f);
            int step = count - 1 - i;
            Wall(parent, "tường trái", max, c, WallDrop, BackWallOrder + 1 + step);
            Wall(parent, "tường phải", c, max, WallDrop, BackWallOrder + 1 + step);
            Wall(parent, "tường trái", min, c, WallDrop, FrontWallOrder + 1 + step);
            if (i != GateSegment)
                Wall(parent, "tường phải", c, min, WallDrop, FrontWallOrder + 1 + step);
        }

        Wall(parent, "cột1", max, max, cornerDrop, BackWallOrder + count + 1);
        Wall(parent, "cột4", min, max, cornerDrop, FrontWallOrder + count + 1);
        Wall(parent, "cột2", max, min, cornerDrop, FrontWallOrder + count + 1);
        Wall(parent, "cột3", min, min, cornerDrop, FrontWallOrder + count + 2);
    }

    /// <summary>Cổng thật của game (ImmigrationManager tự tìm thấy, bấm vào mở kiểm duyệt).</summary>
    private static void BuildGate(Transform root)
    {
        Transform gate = Group(root, GateName);
        gate.localPosition = FromGrid(GateCell.x, GateCell.y);

        float c = -0.5f + SegmentCells * (GateSegment + 0.5f);
        Vector2 origin = gate.localPosition;
        // ImmigrationGate cộng thêm thứ tự theo ô lúc chạy -> lưu phần chênh để nằm trên tường trước
        int gridOrder = 100 + 4 * GridSize - 2 * (GateCell.x + GateCell.y) - 2;
        int order = FrontWallOrder + 20 - gridOrder;
        SpriteRenderer gateSprite = Put(gate, "cổng", FromGrid(c, -0.5f) + Vector2.down * WallDrop - origin, 1.5f, order);
        SpriteRenderer boothSprite = Put(gate, "bốt quan sát", FromGrid(c + 2.1f, -1.5f) + Vector2.down * 0.3f - origin, 0.95f, order + 1);

        // Vùng bấm ôm sát cổng (hình bình hành theo thế isometric) + bốt quan sát
        var col = gate.gameObject.AddComponent<PolygonCollider2D>();
        col.pathCount = 2;
        col.SetPath(0, SpritePath(gateSprite, new Vector2(0f, 0f), new Vector2(0.2f, 0f), new Vector2(1f, 0.3f),
            new Vector2(1f, 1f), new Vector2(0.78f, 1f), new Vector2(0f, 0.69f)));
        col.SetPath(1, SpritePath(boothSprite, new Vector2(0.5f, 0f), new Vector2(1f, 0.15f), new Vector2(1f, 0.85f),
            new Vector2(0.5f, 1f), new Vector2(0f, 0.85f), new Vector2(0f, 0.15f)));
        gate.gameObject.AddComponent<ImmigrationGate>();
    }

    /// <summary>Đổi điểm chuẩn hoá (0..1 trên ảnh, gốc dưới-trái) sang toạ độ local của object cha.</summary>
    private static Vector2[] SpritePath(SpriteRenderer sr, params Vector2[] points)
    {
        Sprite sprite = sr.sprite;
        for (int i = 0; i < points.Length; i++)
        {
            Vector2 local = (points[i] * sprite.rect.size - sprite.pivot) / sprite.pixelsPerUnit;
            points[i] = (Vector2)sr.transform.localPosition + local * (Vector2)sr.transform.localScale;
        }
        return points;
    }

    private static List<Vector2Int> BuildLayout(Transform root)
    {
        var dirt = new List<Vector2Int>();
        Transform flat = Group(root, "Roads"), back = Group(root, "Back"), front = Group(root, "Front");
        int nFlat = 0, nBack = 0, nFront = 0;

        foreach (string raw in File.ReadAllLines(LayoutPath, Encoding.UTF8))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line[0] == '#')
                continue;
            string[] p = line.Split('|');
            if (p[0] == "dirt")
            {
                foreach (string cell in p[1].Split(' '))
                {
                    string[] xy = cell.Split(',');
                    dirt.Add(new Vector2Int(int.Parse(xy[0]), int.Parse(xy[1])));
                }
                continue;
            }

            Vector2 pos = FromMockup(F(p[2]), F(p[3]));
            float height = F(p[4]) / MockupPpu;
            SpriteRenderer sr;
            if (p[0] == "flat") sr = Put(flat, p[1], pos, height, FlatOrder + nFlat++);
            else if (p[0] == "back") sr = Put(back, p[1], pos, height, BackOrder + nBack++);
            else sr = Put(front, p[1], pos, height, FrontOrder + nFront++);

            if (sr != null && p[5] == "D")
                sr.color = ForegroundTint;
        }
        return dirt;
    }

    private static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);

    /// <summary>Mảng đất trong thành theo mockup; giữ búi cỏ cũ ở các ô cỏ.</summary>
    private static void UpdateMapData(List<Vector2Int> dirt)
    {
        MapData data = AssetDatabase.LoadAssetAtPath<MapData>(MapDataPath);
        var cells = new HashSet<Vector2Int>(dirt);
        data.decors.RemoveAll(d => d.decorId == DirtId || cells.Contains(new Vector2Int(d.x, d.y)));
        foreach (Vector2Int c in dirt)
            data.decors.Add(new MapDecorEntry { x = c.x, y = c.y, decorId = DirtId, layer = DecorLayer.Ground, scale = 1f });
        data.hasGate = true;
        data.gateX = GateCell.x;
        data.gateY = GateCell.y;
        EditorUtility.SetDirty(data);
    }

    private static void ConfigureCamera()
    {
        IsoTestCamera cam = Object.FindFirstObjectByType<IsoTestCamera>();
        if (cam == null)
            return;
        Vector2 min = FromMockup(BasePx.xMin, BasePx.yMax), max = FromMockup(BasePx.xMax, BasePx.yMin);
        cam.viewBounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        EditorUtility.SetDirty(cam);
    }
}
