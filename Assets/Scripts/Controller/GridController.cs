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

/// <summary>Trạng thái gameplay của 1 ô lưới.</summary>
[Serializable]
public struct GridCell
{
    /// <summary>Ô đã bị công trình chiếm — cư dân phải đi né.</summary>
    public bool IsOccupied;
    /// <summary>Id công trình đang chiếm ô (0 = trống).</summary>
    public ushort OccupantBuildingId;
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

    [Header("Sinh map tự nhiên")]
    [Tooltip("Id decor đất (mảng đất, lối mòn) trong danh sách Decor")]
    [SerializeField] private string dirtDecorId = "Dirt";
    [Tooltip("Id decor búi cỏ trong danh sách Decor")]
    [SerializeField] private string grassDecorId = "GrassTuft";
    [Tooltip("Ngưỡng noise để thành mảng đất (cao = ít đất)")]
    [SerializeField, Range(0.5f, 0.95f)] private float dirtPatchThreshold = 0.7f;
    [SerializeField, Range(0.02f, 0.4f)] private float dirtPatchScale = 0.16f;
    [Tooltip("Số lối mòn đất chạy từ mép bản đồ vào gần trung tâm")]
    [SerializeField, Range(0, 6)] private int trailCount = 2;
    [Tooltip("Mật độ búi cỏ tối đa ở vùng cỏ rậm")]
    [SerializeField, Range(0f, 1f)] private float grassMaxDensity = 0.55f;
    [SerializeField, Range(0.02f, 0.4f)] private float grassClusterScale = 0.2f;
    [Tooltip("Bán kính khu trung tâm để trống cho người chơi xây thành")]
    [SerializeField, Min(0)] private int clearingRadius = 5;
    [Tooltip("Sắc cỏ thay đổi theo vùng (0 = phẳng một màu)")]
    [SerializeField, Range(0f, 0.3f)] private float tintStrength = 0.16f;
    [Tooltip("Số ô tối dần ở mép bản đồ")]
    [SerializeField, Range(0, 8)] private int edgeVignette = 4;

    [Header("Map Data")]
    [SerializeField] private MapData mapData;
    [SerializeField] private bool saveWhenGenerated = true;
    [Tooltip("Tắt để luôn sinh map mới khi Start (scene test)")]
    [SerializeField] private bool loadSavedMapOnStart = true;

    [Header("Sorting")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int baseOrder = 0;
    [SerializeField] private int groundDecorOrder = 1;
    [SerializeField] private int objectOrder = 100;

    private Transform baseRoot;
    private Transform groundRoot;
    private Transform objectRoot;

    [Header("Base Mesh (gộp tile nền)")]
    [Tooltip("Số ô mỗi cạnh của 1 chunk mesh nền. 1 chunk = 1 renderer")]
    [SerializeField, Min(4)] private int baseChunkSize = 16;
    [Tooltip("Để trống = dùng material sprite mặc định của render pipeline")]
    [SerializeField] private Material baseTileMaterial;

    private sealed class BaseChunk
    {
        public Mesh mesh;
        public MeshRenderer renderer;
        public Color32[] colors;
        public bool colorsDirty;
    }

    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static Material defaultSpriteMaterial;

    private bool gridGenerated;
    private BaseChunk[] baseChunks;
    private int baseChunksX;
    private int[] baseTileChunk;        // index chunk của từng ô
    private int[] baseTileVertexStart;  // vertex đầu tiên của ô trong mesh chunk
    private int baseTileVertexCount;
    private Color32[] baseTileColors;   // Lớp tô màu overlay (debug/highlight), mặc định trắng
    private Color32[] baseTint;         // Lớp sắc tự nhiên của cỏ, sinh từ seed
    private SpriteRenderer[,] groundDecors;
    private SpriteRenderer[,] objectDecors;
    private MapDecorEntry[,] groundEntries;
    private MapDecorEntry[,] objectEntries;

    // Occupancy là state gameplay, không gắn với visual → không bị xoá khi ClearGrid/GenerateGrid
    private GridCell[] cells;
    private int cellsWidth;
    private int cellsHeight;

    /// <summary>Tăng mỗi khi occupancy thay đổi — cư dân dùng để biết cần tìm lại đường.</summary>
    public int OccupancyVersion { get; private set; }

    /// <summary>Ô đặt Cổng thành (ngay ngoài mép, đầu lối mòn). Có thể nằm ngoài lưới.</summary>
    public Vector2Int GateCell { get; private set; }

    /// <summary>Phát sau khi sinh / tải xong bản đồ (Cổng dùng để đặt lại vị trí).</summary>
    public static event Action<GridController> MapGenerated;

    public int Width => width;
    public int Height => height;
    public Vector2 TileSize => tileSize;
    public MapData Data => mapData;
    public Sprite BaseSprite => baseSprite;
    public string SortingLayerName => sortingLayerName;

    private void Start()
    {
        if (loadSavedMapOnStart && mapData == null)
        {
            mapData = Resources.Load<MapData>(DefaultMapResourcePath);
        }

        if (loadSavedMapOnStart && mapData != null && mapData.HasData)
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
        if (useRandomSeed)
            seed = Environment.TickCount & 0x7FFFFFFF;

        GenerateGrid();
        GenerateDecor();

        if (saveWhenGenerated)
        {
            SaveMap();
        }

        MapGenerated?.Invoke(this);
    }

    [ContextMenu("Save Map")]
    public void SaveMap()
    {
        if (!gridGenerated)
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
        mapData.hasGate = true;
        mapData.gateX = GateCell.x;
        mapData.gateY = GateCell.y;

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

        GateCell = mapData.hasGate ? new Vector2Int(mapData.gateX, mapData.gateY) : DefaultGateCell();
        MapGenerated?.Invoke(this);
    }

    private Vector2Int DefaultGateCell() => new Vector2Int(width / 2, -1);

    [ContextMenu("Clear Map")]
    public void ClearGrid()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroySafe(transform.GetChild(i).gameObject);
        }

        DestroyBaseMeshes();
        gridGenerated = false;
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

        groundDecors = new SpriteRenderer[width, height];
        objectDecors = new SpriteRenderer[width, height];
        groundEntries = new MapDecorEntry[width, height];
        objectEntries = new MapDecorEntry[width, height];

        BuildBaseMeshes();
        gridGenerated = true;
        ApplyNaturalTint();
    }

    // ===== Nền: gộp toàn bộ tile thành vài mesh (mỗi chunk 1 renderer) thay vì 1 SpriteRenderer / ô =====

    public bool IsGenerated => gridGenerated;

    /// <summary>Tô màu 1 ô nền (debug overlay, highlight...). Màu nhân với texture qua vertex color.</summary>
    public void SetBaseTileColor(int x, int y, Color color)
    {
        if (!gridGenerated || baseTileColors == null || !IsInside(x, y))
            return;

        int tile = y * width + x;
        Color32 c = color;
        if (baseTileColors[tile].Equals(c))
            return;

        baseTileColors[tile] = c;
        WriteTileVertexColor(tile);
    }

    /// <summary>Màu đỉnh thật = overlay × sắc tự nhiên.</summary>
    private void WriteTileVertexColor(int tile)
    {
        Color32 overlay = baseTileColors[tile];
        Color32 tint = baseTint != null ? baseTint[tile] : new Color32(255, 255, 255, 255);
        Color32 c = new Color32(
            (byte)(overlay.r * tint.r / 255),
            (byte)(overlay.g * tint.g / 255),
            (byte)(overlay.b * tint.b / 255),
            overlay.a);

        BaseChunk chunk = baseChunks[baseTileChunk[tile]];
        int start = baseTileVertexStart[tile];
        for (int v = 0; v < baseTileVertexCount; v++)
            chunk.colors[start + v] = c;
        chunk.colorsDirty = true;
    }

    /// <summary>
    /// Sắc cỏ thay đổi theo vùng (sáng/tối, ngả vàng/xanh) + tối dần ở mép để bản đồ có khung.
    /// Tính từ seed nên lần tải lại cho kết quả y hệt.
    /// </summary>
    private void ApplyNaturalTint()
    {
        int count = width * height;
        baseTint = new Color32[count];
        float ox = (seed % 997) * 1.31f + 11.7f;
        float oy = (seed % 991) * 0.73f + 5.3f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float light = Mathf.PerlinNoise(x * 0.09f + ox, y * 0.09f + oy);
                float warm = Mathf.PerlinNoise(x * 0.06f + oy + 40f, y * 0.06f + ox + 40f);
                float detail = Mathf.PerlinNoise(x * 0.45f + ox + 80f, y * 0.45f + oy + 80f);

                // Đặt mốc dưới 1 để cả vùng sáng lẫn tối đều thấy được (màu đỉnh bị kẹp ở 1)
                float brightness = 1f - tintStrength * 0.6f
                    + (light - 0.5f) * 2f * tintStrength + (detail - 0.5f) * tintStrength * 0.5f;
                float w = (warm - 0.5f) * 2f * tintStrength;   // >0: ngả vàng khô, <0: xanh đậm
                Color c = new Color(brightness * (1f + w * 0.6f), brightness, brightness * (1f - w * 0.9f), 1f);

                if (edgeVignette > 0)
                {
                    int edge = Mathf.Min(Mathf.Min(x, y), Mathf.Min(width - 1 - x, height - 1 - y));
                    if (edge < edgeVignette)
                    {
                        float t = (float)edge / edgeVignette;
                        c *= Mathf.Lerp(0.72f, 1f, t * t);
                        c.a = 1f;
                    }
                }

                // Sprite đã sáng sẵn: kẹp về <= 1 (màu đỉnh không làm sáng hơn texture)
                baseTint[y * width + x] = new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), 1f);
            }
        }

        for (int i = 0; i < count; i++)
            WriteTileVertexColor(i);
    }

    public Color GetBaseTileColor(int x, int y)
    {
        return baseTileColors != null && IsInside(x, y) ? (Color)baseTileColors[y * width + x] : Color.white;
    }

    private void LateUpdate()
    {
        if (baseChunks == null)
            return;

        for (int i = 0; i < baseChunks.Length; i++)
        {
            BaseChunk chunk = baseChunks[i];
            if (chunk == null || !chunk.colorsDirty)
                continue;
            chunk.mesh.colors32 = chunk.colors;
            chunk.colorsDirty = false;
        }
    }

    private void BuildBaseMeshes()
    {
        // Sprite trong atlas: sprite.texture / sprite.uv trả về atlas + UV đã pack
        Vector2[] spriteVerts = baseSprite.vertices;
        Vector2[] spriteUvs = baseSprite.uv;
        ushort[] spriteTris = baseSprite.triangles;
        baseTileVertexCount = spriteVerts.Length;

        int chunk = Mathf.Max(4, baseChunkSize);
        baseChunksX = (width + chunk - 1) / chunk;
        int chunksY = (height + chunk - 1) / chunk;
        baseChunks = new BaseChunk[baseChunksX * chunksY];

        int tileCount = width * height;
        baseTileChunk = new int[tileCount];
        baseTileVertexStart = new int[tileCount];
        baseTileColors = new Color32[tileCount];
        for (int i = 0; i < tileCount; i++)
            baseTileColors[i] = new Color32(255, 255, 255, 255);

        Material material = ResolveBaseMaterial();
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        block.SetTexture(MainTexId, baseSprite.texture);

        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();

        for (int cy = 0; cy < chunksY; cy++)
        {
            for (int cx = 0; cx < baseChunksX; cx++)
            {
                int x0 = cx * chunk, y0 = cy * chunk;
                int x1 = Mathf.Min(width, x0 + chunk), y1 = Mathf.Min(height, y0 + chunk);
                int chunkIndex = cy * baseChunksX + cx;

                verts.Clear();
                uvs.Clear();
                tris.Clear();

                // Vẽ từ xa (x+y lớn) tới gần để phần chồng mép tile giống cách sort cũ
                for (int sum = (x1 - 1) + (y1 - 1); sum >= x0 + y0; sum--)
                {
                    for (int x = x0; x < x1; x++)
                    {
                        int y = sum - x;
                        if (y < y0 || y >= y1)
                            continue;

                        int tile = y * width + x;
                        int start = verts.Count;
                        baseTileChunk[tile] = chunkIndex;
                        baseTileVertexStart[tile] = start;

                        Vector3 offset = GridToLocal(x, y);
                        for (int v = 0; v < spriteVerts.Length; v++)
                        {
                            verts.Add(offset + (Vector3)spriteVerts[v]);
                            uvs.Add(spriteUvs[v]);
                        }
                        for (int t = 0; t < spriteTris.Length; t++)
                            tris.Add(start + spriteTris[t]);
                    }
                }

                Mesh mesh = new Mesh { name = $"BaseChunk_{cx}_{cy}", hideFlags = HideFlags.DontSave };
                if (verts.Count > 65535)
                    mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(verts);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(tris, 0);

                Color32[] colors = new Color32[verts.Count];
                for (int i = 0; i < colors.Length; i++)
                    colors[i] = new Color32(255, 255, 255, 255);
                mesh.colors32 = colors;
                mesh.RecalculateBounds();

                GameObject go = new GameObject(mesh.name);
                go.transform.SetParent(baseRoot, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = material;
                mr.SetPropertyBlock(block);
                mr.sortingLayerName = sortingLayerName;
                mr.sortingOrder = baseOrder;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;

                baseChunks[chunkIndex] = new BaseChunk { mesh = mesh, renderer = mr, colors = colors };
            }
        }
    }

    private void DestroyBaseMeshes()
    {
        if (baseChunks != null)
        {
            for (int i = 0; i < baseChunks.Length; i++)
            {
                if (baseChunks[i] == null || baseChunks[i].mesh == null)
                    continue;
                if (Application.isPlaying)
                    Destroy(baseChunks[i].mesh);
                else
                    DestroyImmediate(baseChunks[i].mesh);
            }
        }

        baseChunks = null;
        baseTileChunk = null;
        baseTileVertexStart = null;
        baseTileColors = null;
    }

    private void OnDestroy()
    {
        DestroyBaseMeshes();
    }

    private Material ResolveBaseMaterial()
    {
        if (baseTileMaterial != null)
            return baseTileMaterial;

        // Lấy đúng material sprite mặc định của pipeline hiện tại (URP 2D: Sprite-Unlit-Default)
        if (defaultSpriteMaterial == null)
        {
            GameObject probe = new GameObject("SpriteMaterialProbe") { hideFlags = HideFlags.HideAndDontSave };
            defaultSpriteMaterial = probe.AddComponent<SpriteRenderer>().sharedMaterial;
            DestroyImmediate(probe);
        }

        return defaultSpriteMaterial;
    }

    /// <summary>
    /// Sinh decor tự nhiên theo seed (thay vì rải ngẫu nhiên từng ô):
    /// mảng đất theo noise + lối mòn uốn lượn từ mép vào trung tâm, búi cỏ mọc thành cụm,
    /// khu trung tâm thoáng để xây thành. Decor khác (nếu thêm vào danh sách) vẫn rải theo chance như cũ.
    /// </summary>
    public void GenerateDecor()
    {
        if (!gridGenerated)
        {
            return;
        }

        Random rng = new Random(seed);
        TileDecor dirt = FindDecor(dirtDecorId);
        TileDecor grass = FindDecor(grassDecorId);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                RemoveDecor(x, y, DecorLayer.Ground);
                RemoveDecor(x, y, DecorLayer.Object);
            }
        }

        bool[,] dirtMask = dirt != null ? BuildDirtMask(rng) : new bool[width, height];

        float gx = (seed % 977) * 0.91f + 23.1f;
        float gy = (seed % 983) * 1.17f + 7.9f;
        Vector2 center = new Vector2((width - 1) * 0.5f, (height - 1) * 0.5f);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (dirtMask[x, y])
                {
                    PlaceDecor(x, y, dirt, rng);
                    continue;
                }

                if (grass != null)
                {
                    // Cụm cỏ: noise mượt, rậm hơn ở mép, thưa ở khu trung tâm
                    float n = Mathf.PerlinNoise(x * grassClusterScale + gx, y * grassClusterScale + gy);
                    float cluster = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.8f, n));
                    float density = 0.03f + grassMaxDensity * cluster;

                    int edge = Mathf.Min(Mathf.Min(x, y), Mathf.Min(width - 1 - x, height - 1 - y));
                    if (edge < 3)
                        density += 0.15f;
                    if (Vector2.Distance(new Vector2(x, y), center) <= clearingRadius)
                        density *= 0.3f;

                    if (rng.NextDouble() < density)
                    {
                        PlaceDecor(x, y, grass, rng);
                        continue;
                    }
                }

                // Decor khác (không phải đất / cỏ) giữ cách rải cũ
                TileDecor other = PickOtherDecor(DecorLayer.Ground, rng);
                if (other != null)
                    PlaceDecor(x, y, other, rng);
            }
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                TileDecor obj = PickOtherDecor(DecorLayer.Object, rng);
                if (obj != null)
                    PlaceDecor(x, y, obj, rng);
            }
        }
    }

    /// <summary>Mặt nạ đất: mảng theo noise + lối mòn, làm mượt để không còn ô đất lẻ.</summary>
    private bool[,] BuildDirtMask(Random rng)
    {
        bool[,] mask = new bool[width, height];
        float ox = (seed % 971) * 0.77f + 51.3f;
        float oy = (seed % 967) * 1.09f + 19.7f;
        Vector2 center = new Vector2((width - 1) * 0.5f, (height - 1) * 0.5f);

        // 1. Mảng đất theo noise (tránh khu trung tâm và sát mép)
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (Vector2.Distance(new Vector2(x, y), center) <= clearingRadius + 1)
                    continue;
                float n = Mathf.PerlinNoise(x * dirtPatchScale + ox, y * dirtPatchScale + oy);
                mask[x, y] = n > dirtPatchThreshold;
            }
        }

        // 2. Làm mượt kiểu cellular automata: bỏ ô lẻ, lấp chỗ khuyết
        mask = SmoothMask(mask, 2);

        // 3. Lối mòn uốn lượn từ mép bản đồ tới rìa khu trung tâm (lối đầu tiên dẫn tới Cổng thành)
        GateCell = DefaultGateCell();
        for (int t = 0; t < trailCount; t++)
            CarveTrail(mask, rng, center, t);

        return mask;
    }

    private bool[,] SmoothMask(bool[,] mask, int passes)
    {
        for (int p = 0; p < passes; p++)
        {
            bool[,] next = new bool[width, height];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    int n = 0;
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dy = -1; dy <= 1; dy++)
                            if ((dx != 0 || dy != 0) && IsInside(x + dx, y + dy) && mask[x + dx, y + dy])
                                n++;
                    next[x, y] = mask[x, y] ? n >= 3 : n >= 5;
                }
            }
            mask = next;
        }
        return mask;
    }

    /// <summary>Đi từ một mép (chia đều theo chỉ số) tới rìa khu trung tâm, có lắc ngẫu nhiên để uốn lượn.</summary>
    private void CarveTrail(bool[,] mask, Random rng, Vector2 center, int index)
    {
        int side = (index + rng.Next(4)) % 4;
        int pos = rng.Next(Mathf.Max(1, width / 4), Mathf.Max(2, width * 3 / 4));
        Vector2Int cur = side switch
        {
            0 => new Vector2Int(pos, 0),
            1 => new Vector2Int(width - 1, pos),
            2 => new Vector2Int(pos, height - 1),
            _ => new Vector2Int(0, pos)
        };

        if (index == 0)
        {
            GateCell = side switch
            {
                0 => new Vector2Int(cur.x, -1),
                1 => new Vector2Int(width, cur.y),
                2 => new Vector2Int(cur.x, height),
                _ => new Vector2Int(-1, cur.y)
            };
        }

        int guard = width * height;
        while (guard-- > 0)
        {
            mask[cur.x, cur.y] = true;

            // Lối mòn chỗ rộng chỗ hẹp cho đỡ răng cưa
            if (rng.NextDouble() < 0.35)
            {
                Vector2Int side2 = rng.Next(4) switch
                {
                    0 => Vector2Int.up,
                    1 => Vector2Int.down,
                    2 => Vector2Int.left,
                    _ => Vector2Int.right
                };
                Vector2Int w = cur + side2;
                if (IsInside(w.x, w.y))
                    mask[w.x, w.y] = true;
            }
            Vector2 toCenter = center - (Vector2)cur;
            if (toCenter.magnitude <= clearingRadius)
                break;

            // Ưu tiên bước về phía trung tâm, thỉnh thoảng rẽ ngang để uốn
            Vector2Int step;
            bool alongX = Mathf.Abs(toCenter.x) > Mathf.Abs(toCenter.y);
            if (rng.NextDouble() < 0.3)
                alongX = !alongX;
            if (alongX)
                step = new Vector2Int(toCenter.x > 0 ? 1 : -1, 0);
            else
                step = new Vector2Int(0, toCenter.y > 0 ? 1 : -1);

            Vector2Int next = cur + step;
            if (!IsInside(next.x, next.y))
                break;
            cur = next;
        }
    }

    /// <summary>Decor cùng layer nhưng KHÔNG phải đất / cỏ (đã sinh riêng).</summary>
    private TileDecor PickOtherDecor(DecorLayer layer, Random rng)
    {
        foreach (TileDecor decor in decors)
        {
            if (decor.layer != layer || decor.sprite == null)
                continue;
            if (decor.Key == dirtDecorId || decor.Key == grassDecorId)
                continue;
            if (rng.NextDouble() < decor.chance)
                return decor;
        }
        return null;
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
        if (decor == null || decor.sprite == null || !IsInside(x, y) || !gridGenerated)
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
        if (!IsInside(x, y) || !gridGenerated)
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
        return GetFootprintCenterWorld(origin, new Vector2Int(size, size));
    }

    public Vector3 GetFootprintCenterWorld(Vector2Int origin, Vector2Int size)
    {
        Vector3 first = GridToLocal(origin.x, origin.y);
        Vector3 last = GridToLocal(origin.x + size.x - 1, origin.y + size.y - 1);
        return transform.TransformPoint((first + last) * 0.5f);
    }

    /// <summary>Góc dưới cùng (góc trước) của footprint, nơi đặt pivot của sprite công trình.</summary>
    public Vector3 GetFootprintAnchorWorld(Vector2Int origin)
    {
        Vector3 local = GridToLocal(origin.x, origin.y) - new Vector3(0f, tileSize.y * 0.5f, 0f);
        return transform.TransformPoint(local);
    }

    /// <summary>Sorting theo tâm footprint (hỗ trợ footprint chữ nhật), cùng thang với GetObjectSortingOrder.</summary>
    public int GetFootprintSortingOrder(Vector2Int origin, Vector2Int size)
    {
        int centerSum = (2 * origin.x + size.x) + (2 * origin.y + size.y);
        return objectOrder + 2 * (width + height) - centerSum;
    }

    /// <summary>
    /// Sorting order cho vật di chuyển tự do (resident) theo vị trí world liên tục, cùng thang với cây/building.
    /// Cây/building luôn nhận order chẵn; vật di chuyển nhận order lẻ nằm giữa:
    /// đứng phía trước tâm vật (x+y nhỏ hơn) → order cao hơn → vẽ đè lên; đứng ngay tâm hoặc phía sau → bị che.
    /// </summary>
    public int GetDynamicSortingOrder(Vector3 worldPos)
    {
        Vector3 local = transform.InverseTransformPoint(worldPos);
        float sum = local.y / (tileSize.y * 0.5f); // = x + y (toạ độ ô liên tục)
        float v = objectOrder + 2 * (width + height) - 2 - 2f * sum;
        return 2 * Mathf.CeilToInt(v * 0.5f) - 1;
    }

    public int SortingLayerId => SortingLayer.NameToID(sortingLayerName);

    public int GetObjectSortingOrder(int x, int y)
    {
        return objectOrder + GetDepth(x, y);
    }

    public bool IsInside(int x, int y)
    {
        return x >= 0 && y >= 0 && x < width && y < height;
    }

    // ===== Occupancy theo ô =====

    public GridCell GetCell(int x, int y)
    {
        EnsureCells();
        return IsInside(x, y) ? cells[y * width + x] : default;
    }

    public bool IsOccupied(int x, int y)
    {
        EnsureCells();
        return IsInside(x, y) && cells[y * width + x].IsOccupied;
    }

    public ushort GetOccupantId(int x, int y)
    {
        EnsureCells();
        return IsInside(x, y) ? cells[y * width + x].OccupantBuildingId : (ushort)0;
    }

    /// <summary>Đánh dấu ô bị công trình <paramref name="buildingId"/> chiếm (0 = giải phóng ô).</summary>
    public void SetOccupied(int x, int y, ushort buildingId)
    {
        EnsureCells();
        if (!IsInside(x, y))
            return;

        ref GridCell cell = ref cells[y * width + x];
        bool occupied = buildingId != 0;
        if (cell.IsOccupied == occupied && cell.OccupantBuildingId == buildingId)
            return;

        cell.IsOccupied = occupied;
        cell.OccupantBuildingId = buildingId;
        OccupancyVersion++;
    }

    /// <summary>
    /// Ô đường: ghi id công trình (để click / phá dỡ / chặn xây chồng) nhưng KHÔNG chặn đường đi của cư dân.
    /// </summary>
    public void SetRoad(int x, int y, ushort buildingId)
    {
        EnsureCells();
        if (!IsInside(x, y))
            return;

        ref GridCell cell = ref cells[y * width + x];
        if (!cell.IsOccupied && cell.OccupantBuildingId == buildingId)
            return;

        cell.IsOccupied = false;
        cell.OccupantBuildingId = buildingId;
        OccupancyVersion++;
    }

    /// <summary>Thứ tự vẽ cho đường: ngay trên nền đất, dưới cư dân và công trình.</summary>
    public int RoadSortingOrder => groundDecorOrder + 1;

    public void SetOccupied(Vector2Int origin, Vector2Int size, ushort buildingId)
    {
        for (int dx = 0; dx < size.x; dx++)
            for (int dy = 0; dy < size.y; dy++)
                SetOccupied(origin.x + dx, origin.y + dy, buildingId);
    }

    public void ClearOccupied(Vector2Int origin, Vector2Int size)
    {
        SetOccupied(origin, size, 0);
    }

    public void ClearAllOccupancy()
    {
        EnsureCells();
        Array.Clear(cells, 0, cells.Length);
        OccupancyVersion++;
    }

    // Cấp phát / resize mảng ô, giữ lại dữ liệu cũ trong vùng giao nhau
    private void EnsureCells()
    {
        int w = Mathf.Max(1, width);
        int h = Mathf.Max(1, height);
        if (cells != null && cellsWidth == w && cellsHeight == h)
            return;

        GridCell[] next = new GridCell[w * h];
        if (cells != null)
        {
            int copyW = Mathf.Min(w, cellsWidth);
            int copyH = Mathf.Min(h, cellsHeight);
            for (int y = 0; y < copyH; y++)
                for (int x = 0; x < copyW; x++)
                    next[y * w + x] = cells[y * cellsWidth + x];
        }

        cells = next;
        cellsWidth = w;
        cellsHeight = h;
        OccupancyVersion++;
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
        // Thang x2 để khớp GetFootprintSortingOrder (1x1 cho cùng giá trị)
        return 2 * (width + height) - 2 * (x + y) - 2;
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
