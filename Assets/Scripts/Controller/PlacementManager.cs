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
    [Range(1, 3)]
    public int size = 1;
    public int costWood;
    public int costGold;
    public GameObject visualPrefab;
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

    private readonly Dictionary<Vector2Int, ushort> buildingGridMatrix = new Dictionary<Vector2Int, ushort>();
    private readonly Dictionary<ushort, BuildingView> buildingViews = new Dictionary<ushort, BuildingView>();
    private readonly List<SpriteRenderer> footprintGhosts = new List<SpriteRenderer>();

    private BuildingView ghostBuilding;
    private BuildingType ghostType = BuildingType.None;
    private Transform ghostRoot;

    public event Action<BuildingData> OnBuildingPlaced;

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

    private void Update()
    {
        if (!isPlacing || Mouse.current == null || database == null || grid == null)
        {
            SetGhostVisible(false);
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

        Vector2Int origin = GetOriginUnderMouse(config.size);
        bool canPlace = CheckAreaAvailable(origin, config.size) && CanAfford(config, out _, out _);

        UpdateGhost(config, origin, canPlace);

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            TryPlaceBuilding(origin, config);
        }
    }

    public void BeginPlacement(BuildingType type)
    {
        currentSelectedType = type;
        isPlacing = true;
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
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                int cx = origin.x + x;
                int cy = origin.y + y;

                if (!grid.IsInside(cx, cy))
                {
                    return false;
                }

                if (buildingGridMatrix.TryGetValue(new Vector2Int(cx, cy), out ushort buildingID) && buildingID != 0)
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
        return buildingGridMatrix.TryGetValue(cell, out ushort id) ? id : (ushort)0;
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
        buildingGridMatrix.Clear();

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
            MarkCells(origin, data.sizeFootprint, data.buildingID);
            SpawnBuildingView(data.buildingID, data.buildingType, origin, data.sizeFootprint);
        }
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
        if (!CheckAreaAvailable(origin, config.size))
        {
            PostPlaceFailed(config.type, BuildingPlaceFailReason.Blocked, 0, 0);
            return false;
        }

        if (!CanAfford(config, out int missingWood, out int missingGold))
        {
            PostPlaceFailed(config.type, BuildingPlaceFailReason.NotEnoughResources, missingWood, missingGold);
            return false;
        }

        BuildingData newData = new BuildingData
        {
            buildingID = GetNextBuildingId(),
            buildingType = config.type,
            coordX = (ushort)origin.x,
            coordY = (ushort)origin.y,
            sizeFootprint = (byte)config.size,
            buildingState = BuildingState.Constructing
        };

        if (buildingManager != null && buildingManager.AddBuilding(newData) == -1)
        {
            PostPlaceFailed(config.type, BuildingPlaceFailReason.CapacityFull, 0, 0);
            return false;
        }

        SpendResources(config);
        MarkCells(origin, config.size, newData.buildingID);
        SpawnBuildingView(newData.buildingID, config.type, origin, config.size);
        OnBuildingPlaced?.Invoke(newData);
        GameEvents.Post(EventID.BuildingPlaced, newData);
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

    private void MarkCells(Vector2Int origin, int size, ushort buildingId)
    {
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                buildingGridMatrix[new Vector2Int(origin.x + x, origin.y + y)] = buildingId;
            }
        }
    }

    private BuildingView SpawnBuildingView(ushort buildingId, BuildingType type, Vector2Int origin, int size)
    {
        BuildingConfig config = database != null ? database.GetConfig(type) : null;
        if (config == null || config.visualPrefab == null)
        {
            return null;
        }

        Vector3 pos = grid.GetFootprintCenterWorld(origin, size);
        GameObject go = Instantiate(config.visualPrefab, pos, Quaternion.identity, buildingRoot);

        BuildingView view = go.GetComponent<BuildingView>();
        if (view == null)
        {
            view = go.AddComponent<BuildingView>();
        }

        view.Init(buildingId, type, origin, size, grid.SortingLayerName, grid.GetObjectSortingOrder(origin.x, origin.y));
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

    private Vector2Int GetOriginUnderMouse(int size)
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
        int offset = size / 2;
        return new Vector2Int(mouseCell.x - offset, mouseCell.y - offset);
    }

    private void UpdateGhost(BuildingConfig config, Vector2Int origin, bool canPlace)
    {
        EnsureGhostRoot();

        if (ghostType != config.type)
        {
            RebuildGhostBuilding(config);
        }

        Color color = canPlace ? validColor : invalidColor;
        int cellCount = config.size * config.size;
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

            int cx = origin.x + i % config.size;
            int cy = origin.y + i / config.size;
            sr.transform.position = grid.GridToWorld(cx, cy);
            sr.color = color;
        }

        if (ghostBuilding != null)
        {
            ghostBuilding.gameObject.SetActive(true);
            ghostBuilding.transform.position = grid.GetFootprintCenterWorld(origin, config.size);
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
