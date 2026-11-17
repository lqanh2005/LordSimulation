using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[Serializable]
public class BuildingConfig
{
    public BuildingType type;
    public string displayName;
    [Tooltip("Footprint vuông dự phòng khi prefab không có IsoFootprint")]
    [Min(1)] public int size = 1;
    public int costWood;
    public int costGold;
    [Tooltip("0 = dùng mặc định theo loại công trình")]
    [Min(0)] public int constructionMonths;
    [Tooltip("0 = dùng mặc định theo loại công trình")]
    [Min(0)] public int maxOccupancy;
    [Tooltip("0 = dùng mặc định theo loại công trình")]
    [Min(0)] public int maxWorkers;
    public GameObject visualPrefab;

    /// <summary>Footprint thật: lấy từ IsoFootprint của prefab, nếu không có thì size x size.</summary>
    public Vector2Int Footprint
    {
        get
        {
            if (visualPrefab != null && visualPrefab.TryGetComponent(out IsoFootprint fp) && fp.size.x > 0 && fp.size.y > 0)
            {
                return fp.size;
            }

            int s = Mathf.Max(1, size);
            return new Vector2Int(s, s);
        }
    }

    /// <summary>Prefab art isometric có pivot ở góc dưới footprint (thay vì tâm).</summary>
    public bool AnchorAtCorner => visualPrefab != null && visualPrefab.GetComponent<IsoFootprint>() != null;
}

public class PlacementManager : MonoBehaviour
{
    [Header("Tham chiếu Hệ thống")]
    public GridController grid;
    public BuildingManager buildingManager;
    public GlobalSystemManager globalSystemManager;
    public Transform buildingRoot;

    [Header("Từ Điển & Điều Khiển")]
    public BuildingDatabase database;
    public BuildingType currentSelectedType = BuildingType.Tent;
    public bool isPlacing = true;
    public bool blockedByTrees = true;

    [Header("Ghost")]
    [SerializeField] private Color validColor = new Color(0.3f, 1f, 0.3f, 0.6f);
    [SerializeField] private Color invalidColor = new Color(1f, 0.2f, 0.2f, 0.6f);
    [SerializeField] private int ghostSortingOrder = 30000;

    [Header("Đang Xây")]
    [SerializeField] private Color constructingColor = new Color(0.7f, 0.7f, 0.7f, 0.55f);

    private readonly Dictionary<ushort, BuildingView> buildingViews = new Dictionary<ushort, BuildingView>();
    private readonly List<SpriteRenderer> footprintGhosts = new List<SpriteRenderer>();

    [Header("Phá Dỡ")]
    [Tooltip("Tỉ lệ hoàn trả tài nguyên khi phá công trình đã xây xong")]
    [SerializeField, Range(0f, 1f)] private float demolishRefundRatio = 0.5f;
    [Tooltip("Tỉ lệ hoàn trả khi phá công trình đang xây dở")]
    [SerializeField, Range(0f, 1f)] private float demolishConstructingRefundRatio = 1f;
    [SerializeField] private Color demolishHoverColor = new Color(1f, 0.35f, 0.35f, 1f);
    [SerializeField] private Color demolishBlockedColor = new Color(0.6f, 0.6f, 0.6f, 1f);

    private bool isDemolishing;
    private ushort demolishHoverId;

    public bool IsDemolishing => isDemolishing;

    private BuildingView ghostBuilding;
    private BuildingType ghostType = BuildingType.None;
    private Transform ghostRoot;

    public event Action<BuildingData> OnBuildingPlaced;

    /// <summary>Tăng mỗi khi occupancy ô thay đổi (lấy từ GridController).</summary>
    public int GridVersion => grid != null ? grid.OccupancyVersion : 0;

    private void Awake()
    {
        if (grid == null)
        {
            grid = FindFirstObjectByType<GridController>();
        }

        if (buildingRoot == null)
        {
            buildingRoot = transform;
        }
    }

    private void Start()
    {
        FarmCropPopup.EnsureInstance(this);
        BuildingInfoPopup.EnsureInstance(this);
        ResidentInfoPopup.EnsureInstance(this);
        NotificationFeed.EnsureInstance(this);
        MerchantPopup.EnsureInstance();
        GameHotkeys.EnsureInstance(this);   // tạo sau cùng để tìm thấy các popup ở trên
    }

    private void OnEnable()
    {
        GameEvents.Unlisten(EventID.BuildingCompleted, OnBuildingCompleted);
        GameEvents.Listen(EventID.BuildingCompleted, OnBuildingCompleted);
    }

    private void OnDisable()
    {
        GameEvents.Unlisten(EventID.BuildingCompleted, OnBuildingCompleted);
    }

    private void OnBuildingCompleted(object param)
    {
        if (param is ushort buildingId && buildingViews.TryGetValue(buildingId, out BuildingView view) && view != null)
        {
            ApplyConstructionVisual(view, false);
        }
    }

    private void ApplyConstructionVisual(BuildingView view, bool constructing)
    {
        if (view != null)
        {
            view.SetTint(constructing ? constructingColor : Color.white);
            RefreshFarmVisual(view);
        }
    }

    // ==========================================
    // RUỘNG FARM
    // ==========================================

    public void RefreshFarmVisuals()
    {
        foreach (BuildingView view in buildingViews.Values)
        {
            RefreshFarmVisual(view);
        }
    }

    public void RefreshFarmVisual(ushort buildingId)
    {
        RefreshFarmVisual(GetBuildingView(buildingId));
    }

    private void RefreshFarmVisual(BuildingView view)
    {
        if (view == null || buildingManager == null || !FarmCropRules.HasCropSlot(view.Type))
        {
            return;
        }

        FarmCropView cropView = view.GetComponent<FarmCropView>();
        int index = buildingManager.FindBuildingIndexById(view.BuildingId);
        if (cropView == null || index < 0)
        {
            return;
        }

        SeasonType season = globalSystemManager != null
            ? globalSystemManager.GetGlobalDataRef().CurrentSeason
            : SeasonType.Spring;
        cropView.Apply(in buildingManager.GetBuildingRef(index), season);
    }

    // Click trái vào công trình khi không ở chế độ xây -> báo cho UI (vd: popup chọn cây trồng của Farm)
    private void HandleBuildingClick()
    {
        if (Mouse.current == null || grid == null || !Mouse.current.leftButton.wasPressedThisFrame || IsPointerOverUI())
        {
            return;
        }

        Vector2Int cell = GetOriginUnderMouse(Vector2Int.one);
        ushort buildingId = GetBuildingIdAt(cell);
        if (buildingId != 0)
        {
            GameEvents.Post(EventID.BuildingClicked, buildingId);
        }
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.xKey.wasPressedThisFrame)
        {
            ToggleDemolish();
        }

        if (isDemolishing)
        {
            SetGhostVisible(false);
            UpdateDemolish();
            return;
        }

        if (!isPlacing || Mouse.current == null || database == null || grid == null)
        {
            SetGhostVisible(false);
            if (!isPlacing)
            {
                HandleBuildingClick();
            }
            return;
        }

        if (Mouse.current.rightButton.wasPressedThisFrame ||
            (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame))
        {
            CancelPlacement();
            return;
        }

        BuildingConfig config = database.GetConfig(currentSelectedType);
        if (config == null)
        {
            SetGhostVisible(false);
            return;
        }

        if (IsPointerOverUI())
        {
            SetGhostVisible(false);
            return;
        }

        Vector2Int footprint = config.Footprint;
        Vector2Int origin = GetOriginUnderMouse(footprint);
        bool canPlace = CheckAreaAvailable(origin, footprint) && CanAfford(config, out _, out _)
                        && !IsUniqueLimitReached(config.type) && IsResearched(config.type);

        UpdateGhost(config, origin, canPlace);

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            TryPlaceBuilding(origin, config);
        }
    }

    public void BeginPlacement(BuildingType type)
    {
        if (!IsResearched(type))
        {
            PostPlaceFailed(type, BuildingPlaceFailReason.NotResearched, 0, 0);
            return;
        }

        CancelDemolish();
        currentSelectedType = type;
        isPlacing = true;
    }

    // ==========================================
    // PHÁ DỠ
    // ==========================================

    public void ToggleDemolish()
    {
        if (isDemolishing)
            CancelDemolish();
        else
            BeginDemolish();
    }

    public void BeginDemolish()
    {
        if (isDemolishing)
            return;

        CancelPlacement();
        isDemolishing = true;
        GameEvents.Post(EventID.DemolishModeChanged, true);
    }

    public void CancelDemolish()
    {
        if (!isDemolishing)
            return;

        SetDemolishHover(0);
        isDemolishing = false;
        GameEvents.Post(EventID.DemolishModeChanged, false);
    }

    /// <summary>Công trình có được phép phá không (Tòa thị chính là lõi của thành phố → không phá).</summary>
    public bool CanDemolish(ushort buildingId)
    {
        if (buildingId == 0 || buildingManager == null)
            return false;

        int index = buildingManager.FindBuildingIndexById(buildingId);
        if (index < 0)
            return false;

        return buildingManager.GetBuildingRef(index).buildingType != BuildingType.CityHall;
    }

    private void UpdateDemolish()
    {
        if (Mouse.current == null || grid == null)
            return;

        if (Mouse.current.rightButton.wasPressedThisFrame ||
            (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame))
        {
            CancelDemolish();
            return;
        }

        if (IsPointerOverUI())
        {
            SetDemolishHover(0);
            return;
        }

        ushort hoverId = GetBuildingIdAt(GetOriginUnderMouse(Vector2Int.one));
        SetDemolishHover(hoverId);

        if (hoverId != 0 && Mouse.current.leftButton.wasPressedThisFrame)
        {
            DemolishBuilding(hoverId);
        }
    }

    private void SetDemolishHover(ushort buildingId)
    {
        if (demolishHoverId == buildingId)
            return;

        if (demolishHoverId != 0)
            RestoreBuildingTint(demolishHoverId);

        demolishHoverId = buildingId;
        if (buildingId != 0 && buildingViews.TryGetValue(buildingId, out BuildingView view) && view != null)
            view.SetTint(CanDemolish(buildingId) ? demolishHoverColor : demolishBlockedColor);
    }

    private void RestoreBuildingTint(ushort buildingId)
    {
        if (!buildingViews.TryGetValue(buildingId, out BuildingView view) || view == null || buildingManager == null)
            return;

        int index = buildingManager.FindBuildingIndexById(buildingId);
        bool constructing = index >= 0 && BuildingRules.IsUnderConstruction(in buildingManager.GetBuildingRef(index));
        view.SetTint(constructing ? constructingColor : Color.white);
    }

    /// <summary>
    /// Phá công trình: giải phóng ô (IsOccupied = false), xoá view + data, hoàn trả một phần tài nguyên,
    /// rồi bắn BuildingDemolished để cư dân bỏ nhà / chỗ làm và tìm đường lại.
    /// </summary>
    public bool DemolishBuilding(ushort buildingId)
    {
        if (!CanDemolish(buildingId))
            return false;

        if (demolishHoverId == buildingId)
            demolishHoverId = 0;

        if (!buildingManager.RemoveBuilding(buildingId, out BuildingData removed))
            return false;

        Vector2Int origin = new Vector2Int(removed.coordX, removed.coordY);
        Vector2Int footprint = BuildingRules.GetFootprint(in removed);
        if (grid != null)
            grid.ClearOccupied(origin, footprint);

        if (buildingViews.TryGetValue(buildingId, out BuildingView view))
        {
            buildingViews.Remove(buildingId);
            if (view != null)
                Destroy(view.gameObject);
        }

        BuildingDemolishedPayload payload = new BuildingDemolishedPayload
        {
            buildingId = buildingId,
            type = removed.buildingType
        };
        RefundResources(in removed, ref payload);

        GameEvents.Post(EventID.BuildingDemolished, payload);
        return true;
    }

    /// <summary>Số gỗ / vàng hoàn lại nếu phá công trình này (dùng cho popup xem trước).</summary>
    public void GetDemolishRefund(in BuildingData building, out int wood, out int gold)
    {
        wood = gold = 0;
        BuildingConfig config = database != null ? database.GetConfig(building.buildingType) : null;
        if (config == null)
            return;

        float ratio = BuildingRules.IsUnderConstruction(in building)
            ? demolishConstructingRefundRatio
            : demolishRefundRatio;
        wood = Mathf.FloorToInt(config.costWood * ratio);
        gold = Mathf.FloorToInt(config.costGold * ratio);
    }

    private void RefundResources(in BuildingData removed, ref BuildingDemolishedPayload payload)
    {
        if (globalSystemManager == null)
            return;

        GetDemolishRefund(in removed, out payload.refundWood, out payload.refundGold);
        if (payload.refundWood == 0 && payload.refundGold == 0)
            return;

        ref GlobalSystemData globalData = ref globalSystemManager.GetGlobalDataRef();
        globalData.stockWood += payload.refundWood;
        globalData.treasuryGold += payload.refundGold;
        GameEvents.Post(EventID.ResourcesChanged);
    }

    public void CancelPlacement()
    {
        isPlacing = false;
        SetGhostVisible(false);
    }

    public bool PlaceAt(Vector3Int originCell3D, BuildingType type)
    {
        return PlaceAt(new Vector2Int(originCell3D.x, originCell3D.y), type);
    }

    public bool PlaceAt(Vector2Int origin, BuildingType type)
    {
        if (database == null || grid == null)
        {
            return false;
        }

        BuildingConfig config = database.GetConfig(type);
        return config != null && TryPlaceBuilding(origin, config);
    }

    public bool CheckAreaAvailable(Vector2Int origin, int size)
    {
        return CheckAreaAvailable(origin, new Vector2Int(size, size));
    }

    public bool CheckAreaAvailable(Vector2Int origin, Vector2Int size)
    {
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                int cx = origin.x + x;
                int cy = origin.y + y;

                if (!grid.IsInside(cx, cy))
                {
                    return false;
                }

                if (grid.IsOccupied(cx, cy))
                {
                    return false;
                }

                if (blockedByTrees && grid.HasObject(cx, cy))
                {
                    return false;
                }
            }
        }

        return true;
    }

    public ushort GetBuildingIdAt(Vector2Int cell)
    {
        return grid != null ? grid.GetOccupantId(cell.x, cell.y) : (ushort)0;
    }

    public BuildingView GetBuildingView(ushort buildingId)
    {
        return buildingViews.TryGetValue(buildingId, out BuildingView view) ? view : null;
    }

    public void RebuildVisuals()
    {
        foreach (BuildingView view in buildingViews.Values)
        {
            if (view != null)
            {
                Destroy(view.gameObject);
            }
        }

        buildingViews.Clear();
        if (grid != null)
            grid.ClearAllOccupancy();

        if (buildingManager == null || grid == null)
        {
            return;
        }

        for (int i = 0; i < buildingManager.activeCount; i++)
        {
            ref BuildingData data = ref buildingManager.GetBuildingRef(i);
            if (data.buildingState == BuildingState.Destroyed)
            {
                continue;
            }

            Vector2Int origin = new Vector2Int(data.coordX, data.coordY);
            Vector2Int footprint = BuildingRules.GetFootprint(in data);
            MarkCells(origin, footprint, data.buildingID);
            BuildingView view = SpawnBuildingView(data.buildingID, data.buildingType, origin, footprint);
            ApplyConstructionVisual(view, BuildingRules.IsUnderConstruction(in data));
        }
    }

    /// <summary>Công trình đã được mở khóa trong cây nghiên cứu chưa.</summary>
    public bool IsResearched(BuildingType type)
    {
        ResearchManager research = GamePlayController.Instance != null && GamePlayController.Instance.playerContain != null
            ? GamePlayController.Instance.playerContain.researchManager
            : null;
        return research == null || research.IsBuildingUnlocked(type);
    }

    /// <summary>Công trình chỉ được xây 1 (vd: CityHall) đã tồn tại.</summary>
    public bool IsUniqueLimitReached(BuildingType type)
    {
        if (!BuildingEffectRules.IsUnique(type) || buildingManager == null)
        {
            return false;
        }

        for (int i = 0; i < buildingManager.activeCount; i++)
        {
            ref BuildingData data = ref buildingManager.GetBuildingRef(i);
            if (data.buildingType == type && data.buildingState != BuildingState.Destroyed)
            {
                return true;
            }
        }

        return false;
    }

    public bool CanAfford(BuildingConfig config, out int missingWood, out int missingGold)
    {
        missingWood = 0;
        missingGold = 0;

        if (config == null || globalSystemManager == null)
        {
            return config != null;
        }

        ref GlobalSystemData globalData = ref globalSystemManager.GetGlobalDataRef();
        missingWood = Mathf.Max(0, config.costWood - globalData.stockWood);
        missingGold = Mathf.Max(0, config.costGold - globalData.treasuryGold);
        return missingWood == 0 && missingGold == 0;
    }

    private bool TryPlaceBuilding(Vector2Int origin, BuildingConfig config)
    {
        if (!IsResearched(config.type))
        {
            PostPlaceFailed(config.type, BuildingPlaceFailReason.NotResearched, 0, 0);
            return false;
        }

        if (IsUniqueLimitReached(config.type))
        {
            PostPlaceFailed(config.type, BuildingPlaceFailReason.AlreadyBuilt, 0, 0);
            return false;
        }

        Vector2Int footprint = config.Footprint;
        if (!CheckAreaAvailable(origin, footprint))
        {
            PostPlaceFailed(config.type, BuildingPlaceFailReason.Blocked, 0, 0);
            return false;
        }

        if (!CanAfford(config, out int missingWood, out int missingGold))
        {
            PostPlaceFailed(config.type, BuildingPlaceFailReason.NotEnoughResources, missingWood, missingGold);
            return false;
        }

        BuildingData newData = BuildingRules.CreatePlaced(config, GetNextBuildingId(), origin);

        if (buildingManager != null && buildingManager.AddBuilding(newData) == -1)
        {
            PostPlaceFailed(config.type, BuildingPlaceFailReason.CapacityFull, 0, 0);
            return false;
        }

        newData.isAllocated = true;
        SpendResources(config);
        MarkCells(origin, footprint, newData.buildingID);
        BuildingView view = SpawnBuildingView(newData.buildingID, config.type, origin, footprint);
        ApplyConstructionVisual(view, BuildingRules.IsUnderConstruction(in newData));
        OnBuildingPlaced?.Invoke(newData);
        GameEvents.Post(EventID.BuildingPlaced, newData);
        if (newData.buildingState == BuildingState.Active)
        {
            GameEvents.Post(EventID.BuildingCompleted, newData.buildingID);
        }
        return true;
    }

    private void SpendResources(BuildingConfig config)
    {
        if (globalSystemManager == null || (config.costWood == 0 && config.costGold == 0))
        {
            return;
        }

        ref GlobalSystemData globalData = ref globalSystemManager.GetGlobalDataRef();
        globalData.stockWood -= config.costWood;
        globalData.treasuryGold -= config.costGold;
        GameEvents.Post(EventID.ResourcesChanged);
    }

    private static void PostPlaceFailed(BuildingType type, BuildingPlaceFailReason reason, int missingWood, int missingGold)
    {
        GameEvents.Post(EventID.BuildingPlaceFailed, new BuildingPlaceFailedPayload
        {
            type = type,
            reason = reason,
            missingWood = missingWood,
            missingGold = missingGold
        });
    }

    private void MarkCells(Vector2Int origin, Vector2Int size, ushort buildingId)
    {
        // Đánh dấu IsOccupied cho từng ô của footprint → cư dân tìm đường sẽ né
        grid.SetOccupied(origin, size, buildingId);
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                // Bỏ decor nền dưới công trình để không lộ qua sprite (nhất là lúc đang xây, bán trong suốt)
                grid.RemoveDecor(origin.x + x, origin.y + y, DecorLayer.Ground);
            }
        }
    }

    private BuildingView SpawnBuildingView(ushort buildingId, BuildingType type, Vector2Int origin, Vector2Int size)
    {
        BuildingConfig config = database != null ? database.GetConfig(type) : null;
        if (config == null || config.visualPrefab == null)
        {
            return null;
        }

        Vector3 pos = GetVisualPosition(config, origin, size);
        GameObject go = Instantiate(config.visualPrefab, pos, Quaternion.identity, buildingRoot);

        BuildingView view = go.GetComponent<BuildingView>();
        if (view == null)
        {
            view = go.AddComponent<BuildingView>();
        }

        view.Init(buildingId, type, origin, size, grid.SortingLayerName, grid.GetFootprintSortingOrder(origin, size));
        buildingViews[buildingId] = view;
        return view;
    }

    private ushort GetNextBuildingId()
    {
        int maxId = 0;

        foreach (ushort id in buildingViews.Keys)
        {
            maxId = Mathf.Max(maxId, id);
        }

        if (buildingManager != null)
        {
            for (int i = 0; i < buildingManager.activeCount; i++)
            {
                maxId = Mathf.Max(maxId, buildingManager.allBuildings[i].buildingID);
            }
        }

        return (ushort)(maxId + 1);
    }

    private Vector3 GetVisualPosition(BuildingConfig config, Vector2Int origin, Vector2Int size)
    {
        return config != null && config.AnchorAtCorner
            ? grid.GetFootprintAnchorWorld(origin)
            : grid.GetFootprintCenterWorld(origin, size);
    }

    private Vector2Int GetOriginUnderMouse(Vector2Int size)
    {
        Camera cam = Camera.main;
        Vector3 mouseWorldPos = Vector3.zero;

        if (cam != null)
        {
            Plane groundPlane = new Plane(Vector3.forward, grid.transform.position);
            Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());

            if (groundPlane.Raycast(ray, out float enter))
            {
                mouseWorldPos = ray.GetPoint(enter);
            }
        }

        Vector2Int mouseCell = grid.WorldToGrid(mouseWorldPos);
        return new Vector2Int(mouseCell.x - size.x / 2, mouseCell.y - size.y / 2);
    }

    private void UpdateGhost(BuildingConfig config, Vector2Int origin, bool canPlace)
    {
        EnsureGhostRoot();

        if (ghostType != config.type)
        {
            RebuildGhostBuilding(config);
        }

        Color color = canPlace ? validColor : invalidColor;
        Vector2Int footprint = config.Footprint;
        int cellCount = footprint.x * footprint.y;
        EnsureFootprintGhosts(cellCount);

        for (int i = 0; i < footprintGhosts.Count; i++)
        {
            SpriteRenderer sr = footprintGhosts[i];
            bool active = i < cellCount;
            sr.gameObject.SetActive(active);

            if (!active)
            {
                continue;
            }

            int cx = origin.x + i % footprint.x;
            int cy = origin.y + i / footprint.x;
            sr.transform.position = grid.GridToWorld(cx, cy);
            sr.color = color;
        }

        if (ghostBuilding != null)
        {
            ghostBuilding.gameObject.SetActive(true);
            ghostBuilding.transform.position = GetVisualPosition(config, origin, footprint);
            ghostBuilding.SetColor(new Color(1f, 1f, 1f, canPlace ? 0.8f : 0.4f));
        }

        ghostRoot.gameObject.SetActive(true);
    }

    private void RebuildGhostBuilding(BuildingConfig config)
    {
        if (ghostBuilding != null)
        {
            Destroy(ghostBuilding.gameObject);
            ghostBuilding = null;
        }

        ghostType = config.type;

        if (config.visualPrefab == null)
        {
            return;
        }

        GameObject go = Instantiate(config.visualPrefab, ghostRoot);
        go.name = $"Ghost_{config.type}";

        ghostBuilding = go.GetComponent<BuildingView>();
        if (ghostBuilding == null)
        {
            ghostBuilding = go.AddComponent<BuildingView>();
        }

        ghostBuilding.SetSorting(grid.SortingLayerName, ghostSortingOrder);
    }

    private void EnsureGhostRoot()
    {
        if (ghostRoot != null)
        {
            return;
        }

        ghostRoot = new GameObject("PlacementGhost").transform;
        ghostRoot.SetParent(transform, false);
    }

    private void EnsureFootprintGhosts(int count)
    {
        while (footprintGhosts.Count < count)
        {
            GameObject go = new GameObject($"Footprint_{footprintGhosts.Count}");
            go.transform.SetParent(ghostRoot, false);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = grid.BaseSprite;
            sr.sortingLayerName = grid.SortingLayerName;
            sr.sortingOrder = ghostSortingOrder - 1;
            footprintGhosts.Add(sr);
        }
    }

    private void SetGhostVisible(bool visible)
    {
        if (ghostRoot != null)
        {
            ghostRoot.gameObject.SetActive(visible);
        }
    }

    private static bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
