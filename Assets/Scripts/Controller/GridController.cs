using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum DecorLayer
{
    Ground,
    Object
}

[Serializable]
public class TileDecor
{
    public string id;
    public Sprite sprite;
    public DecorLayer layer = DecorLayer.Ground;
    [Range(0f, 1f)] public float chance = 0.3f;
    public Vector2 offset = Vector2.zero;
    public Vector2 randomOffset = Vector2.zero;
    public Vector2 scaleRange = Vector2.one;
    public bool randomFlipX;

    public string Key => string.IsNullOrEmpty(id) && sprite != null ? sprite.name : id;
}

public class GridController : MonoBehaviour
{
    private const string DefaultMapResourcePath = "Map/MapData";
    private const string DefaultMapAssetPath = "Assets/Resources/Map/MapData.asset";

    [Header("Grid")]
    [SerializeField] private int width = 10;
    [SerializeField] private int height = 10;
    [SerializeField] private bool autoTileSize = true;
    [SerializeField] private Vector2 tileSize = Vector2.one;
    [SerializeField, Range(0f, 0.1f)] private float tileOverlap = 0.01f;

    [Header("Base")]
    [SerializeField] private Sprite baseSprite;

    [Header("Decor")]
    [SerializeField] private List<TileDecor> decors = new List<TileDecor>();
    [SerializeField] private bool useRandomSeed = true;
    [SerializeField] private int seed;

    [Header("Map Data")]
    [SerializeField] private MapData mapData;
    [SerializeField] private bool saveWhenGenerated = true;

    [Header("Sorting")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int baseOrder = 0;
    [SerializeField] private int groundDecorOrder = 1;
    [SerializeField] private int objectOrder = 100;

    private Transform baseRoot;
    private Transform groundRoot;
    private Transform objectRoot;

    private SpriteRenderer[,] baseTiles;
    private SpriteRenderer[,] groundDecors;
    private SpriteRenderer[,] objectDecors;
    private MapDecorEntry[,] groundEntries;
    private MapDecorEntry[,] objectEntries;

    public int Width => width;
    public int Height => height;
    public Vector2 TileSize => tileSize;
    public MapData Data => mapData;
    public Sprite BaseSprite => baseSprite;
    public string SortingLayerName => sortingLayerName;

    private void Start()
    {
        if (mapData == null)
        {
            mapData = Resources.Load<MapData>(DefaultMapResourcePath);
        }

        if (mapData != null && mapData.HasData)
        {
            LoadMap();
        }
        else
        {
            GenerateNewMap();
        }
    }

    [ContextMenu("Generate New Map")]
    public void GenerateNewMap()
    {
        GenerateGrid();
        GenerateDecor();

        if (saveWhenGenerated)
        {
            SaveMap();
        }
    }

    [ContextMenu("Save Map")]
    public void SaveMap()
    {
        if (baseTiles == null)
        {
            return;
        }

#if UNITY_EDITOR
        if (mapData == null)
        {
            mapData = CreateMapAsset();
        }
#endif
        if (mapData == null)
        {
            return;
        }

        mapData.Clear();
        mapData.width = width;
        mapData.height = height;
        mapData.seed = seed;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (!string.IsNullOrEmpty(groundEntries[x, y].decorId))
                {
                    mapData.decors.Add(groundEntries[x, y]);
                }

                if (!string.IsNullOrEmpty(objectEntries[x, y].decorId))
                {
                    mapData.decors.Add(objectEntries[x, y]);
                }
            }
        }

#if UNITY_EDITOR
        EditorUtility.SetDirty(mapData);
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(this);
        }
        AssetDatabase.SaveAssets();
#endif
    }

    [ContextMenu("Load Map")]
    public void LoadMap()
    {
        if (mapData == null)
        {
            mapData = Resources.Load<MapData>(DefaultMapResourcePath);
        }

        if (mapData == null || !mapData.HasData)
        {
            return;
        }

        width = mapData.width;
        height = mapData.height;
        seed = mapData.seed;

        GenerateGrid();

        foreach (MapDecorEntry entry in mapData.decors)
        {
            TileDecor decor = FindDecor(entry.decorId);
            if (decor != null)
            {
                PlaceDecor(entry.x, entry.y, decor, entry.offset, entry.scale, entry.flipX);
            }
        }
    }

    [ContextMenu("Clear Map")]
    public void ClearGrid()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroySafe(transform.GetChild(i).gameObject);
        }

        baseTiles = null;
        groundDecors = null;
        objectDecors = null;
        groundEntries = null;
        objectEntries = null;
    }

    public void GenerateGrid()
    {
        ClearGrid();

        if (baseSprite == null)
        {
            return;
        }

        if (autoTileSize)
        {
            tileSize = GetOpaqueSize(baseSprite) * (1f - tileOverlap);
        }

        baseRoot = CreateRoot("Base");
        groundRoot = CreateRoot("GroundDecor");
        objectRoot = CreateRoot("ObjectDecor");

        baseTiles = new SpriteRenderer[width, height];
        groundDecors = new SpriteRenderer[width, height];
        objectDecors = new SpriteRenderer[width, height];
        groundEntries = new MapDecorEntry[width, height];
        objectEntries = new MapDecorEntry[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                baseTiles[x, y] = CreateRenderer($"Base_{x}_{y}", baseRoot, baseSprite, GridToLocal(x, y), baseOrder);
            }
        }
    }

    public void GenerateDecor()
    {
        if (baseTiles == null)
        {
            return;
        }

        if (useRandomSeed)
        {
            seed = Environment.TickCount;
        }

        Random rng = new Random(seed);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                RemoveDecor(x, y, DecorLayer.Ground);
                RemoveDecor(x, y, DecorLayer.Object);

                TileDecor ground = PickDecor(DecorLayer.Ground, rng);
                if (ground != null)
                {
                    PlaceDecor(x, y, ground, rng);
                }

                TileDecor obj = PickDecor(DecorLayer.Object, rng);
                if (obj != null)
                {
                    PlaceDecor(x, y, obj, rng);
                }
            }
        }
    }

    public bool PlaceDecor(int x, int y, string decorId)
    {
        return PlaceDecor(x, y, FindDecor(decorId), new Random());
    }

    public bool PlaceDecor(int x, int y, TileDecor decor, Random rng)
    {
        if (decor == null)
        {
            return false;
        }

        Vector2 jitter = new Vector2(
            RandomRange(rng, -decor.randomOffset.x, decor.randomOffset.x),
            RandomRange(rng, -decor.randomOffset.y, decor.randomOffset.y));
        float scale = RandomRange(rng, decor.scaleRange.x, decor.scaleRange.y);
        bool flip = decor.randomFlipX && rng.NextDouble() < 0.5;

        return PlaceDecor(x, y, decor, decor.offset + jitter, scale, flip);
    }

    public bool PlaceDecor(int x, int y, TileDecor decor, Vector2 offset, float scale, bool flipX)
    {
        if (decor == null || decor.sprite == null || !IsInside(x, y) || baseTiles == null)
        {
            return false;
        }

        RemoveDecor(x, y, decor.layer);

        bool isObject = decor.layer == DecorLayer.Object;
        Transform root = isObject ? objectRoot : groundRoot;
        int order = isObject ? objectOrder + GetDepth(x, y) : groundDecorOrder;
        Vector3 pos = GridToLocal(x, y) + (Vector3)offset;

        SpriteRenderer sr = CreateRenderer($"{decor.Key}_{x}_{y}", root, decor.sprite, pos, order);
        sr.transform.localScale = new Vector3(scale, scale, 1f);
        sr.flipX = flipX;

        MapDecorEntry entry = new MapDecorEntry
        {
            x = x,
            y = y,
            decorId = decor.Key,
            layer = decor.layer,
            offset = offset,
            scale = scale,
            flipX = flipX
        };

        if (isObject)
        {
            objectDecors[x, y] = sr;
            objectEntries[x, y] = entry;
        }
        else
        {
            groundDecors[x, y] = sr;
            groundEntries[x, y] = entry;
        }

        return true;
    }

    public void RemoveDecor(int x, int y, DecorLayer layer)
    {
        if (!IsInside(x, y) || baseTiles == null)
        {
            return;
        }

        bool isObject = layer == DecorLayer.Object;
        SpriteRenderer[,] renderers = isObject ? objectDecors : groundDecors;
        MapDecorEntry[,] entries = isObject ? objectEntries : groundEntries;

        if (renderers[x, y] != null)
        {
            DestroySafe(renderers[x, y].gameObject);
            renderers[x, y] = null;
        }

        entries[x, y] = default;
    }

    public bool HasObject(int x, int y)
    {
        return objectDecors != null && IsInside(x, y) && objectDecors[x, y] != null;
    }

    public Vector3 GridToLocal(int x, int y)
    {
        float px = (x - y) * tileSize.x * 0.5f;
        float py = (x + y) * tileSize.y * 0.5f;
        return new Vector3(px, py, 0f);
    }

    public Vector3 GridToWorld(int x, int y)
    {
        return transform.TransformPoint(GridToLocal(x, y));
    }

    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        Vector3 local = transform.InverseTransformPoint(worldPos);
        float a = local.x / (tileSize.x * 0.5f);
        float b = local.y / (tileSize.y * 0.5f);
        int x = Mathf.RoundToInt((a + b) * 0.5f);
        int y = Mathf.RoundToInt((b - a) * 0.5f);
        return new Vector2Int(x, y);
    }

    public Vector3 GetFootprintCenterWorld(Vector2Int origin, int size)
    {
        Vector3 first = GridToLocal(origin.x, origin.y);
        Vector3 last = GridToLocal(origin.x + size - 1, origin.y + size - 1);
        return transform.TransformPoint((first + last) * 0.5f);
    }

    public int GetObjectSortingOrder(int x, int y)
    {
        return objectOrder + GetDepth(x, y);
    }

    public bool IsInside(int x, int y)
    {
        return x >= 0 && y >= 0 && x < width && y < height;
    }

    public SpriteRenderer GetBaseTile(int x, int y)
    {
        return baseTiles != null && IsInside(x, y) ? baseTiles[x, y] : null;
    }

    private TileDecor FindDecor(string decorId)
    {
        return decors.Find(d => d.Key == decorId);
    }

    private TileDecor PickDecor(DecorLayer layer, Random rng)
    {
        foreach (TileDecor decor in decors)
        {
            if (decor.layer != layer || decor.sprite == null)
            {
                continue;
            }

            if (rng.NextDouble() < decor.chance)
            {
                return decor;
            }
        }

        return null;
    }

    private static Vector2 GetOpaqueSize(Sprite sprite)
    {
        Vector2[] vertices = sprite.vertices;
        if (vertices == null || vertices.Length == 0)
        {
            return sprite.bounds.size;
        }

        Vector2 min = vertices[0];
        Vector2 max = vertices[0];
        for (int i = 1; i < vertices.Length; i++)
        {
            min = Vector2.Min(min, vertices[i]);
            max = Vector2.Max(max, vertices[i]);
        }

        return max - min;
    }

    private int GetDepth(int x, int y)
    {
        return (width + height) - (x + y);
    }

    private Transform CreateRoot(string rootName)
    {
        GameObject go = new GameObject(rootName);
        go.transform.SetParent(transform, false);
        return go.transform;
    }

    private SpriteRenderer CreateRenderer(string objName, Transform parent, Sprite sprite, Vector3 localPos, int order)
    {
        GameObject go = new GameObject(objName);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingLayerName = sortingLayerName;
        sr.sortingOrder = order;
        return sr;
    }

    private static void DestroySafe(GameObject go)
    {
        if (Application.isPlaying)
        {
            Destroy(go);
        }
        else
        {
            DestroyImmediate(go);
        }
    }

    private static float RandomRange(Random rng, float min, float max)
    {
        return min + (float)rng.NextDouble() * (max - min);
    }

#if UNITY_EDITOR
    private static MapData CreateMapAsset()
    {
        MapData existing = AssetDatabase.LoadAssetAtPath<MapData>(DefaultMapAssetPath);
        if (existing != null)
        {
            return existing;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        if (!AssetDatabase.IsValidFolder("Assets/Resources/Map"))
        {
            AssetDatabase.CreateFolder("Assets/Resources", "Map");
        }

        MapData asset = ScriptableObject.CreateInstance<MapData>();
        AssetDatabase.CreateAsset(asset, DefaultMapAssetPath);
        return asset;
    }
#endif
}
