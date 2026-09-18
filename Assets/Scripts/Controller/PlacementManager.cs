using System;
using System.Collections.Generic;
using UnityEngine;
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
    public Grid isometricGrid;
    public Transform ghostCursor;
    public BuildingManager buildingManager;
    public GlobalSystemManager globalSystemManager;

    [Header("Từ Điển & Điều Khiển")]
    public BuildingDatabase database;
    public BuildingType currentSelectedType = BuildingType.House;
    private Dictionary<Vector2Int, ushort> buildingGridMatrix = new Dictionary<Vector2Int, ushort>();
    private SpriteRenderer ghostRenderer;

    void Update()
    {
        if (Mouse.current == null || database == null) return;

        BuildingConfig currentConfig = database.GetConfig(currentSelectedType);
        if (currentConfig == null) return;

        Plane groundPlane = new Plane(Vector3.forward, Vector3.zero);
        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        Vector3 mouseWorldPos = Vector3.zero;

        if (groundPlane.Raycast(ray, out float enter))
        {
            mouseWorldPos = ray.GetPoint(enter);
        }
        mouseWorldPos.z = 0f;

        Vector3Int mouseCell = isometricGrid.WorldToCell(mouseWorldPos);
        mouseCell.z = 0;

        int offsetXY = currentConfig.size / 2;
        Vector3Int originCell3D = new Vector3Int(mouseCell.x - offsetXY, mouseCell.y - offsetXY, 0);

        Vector3 basePos = isometricGrid.GetCellCenterWorld(originCell3D);
        Vector3Int topCell3D = originCell3D + new Vector3Int(currentConfig.size - 1, currentConfig.size - 1, 0);
        Vector3 topPos = isometricGrid.GetCellCenterWorld(topCell3D);

        Vector3 trueCenterPos = (basePos + topPos) / 2f;
        trueCenterPos.z = 0f;

        Vector2Int checkOrigin = new Vector2Int(originCell3D.x, originCell3D.y);
        bool canPlaceHere = CheckAreaAvailable(checkOrigin, currentConfig.size);

        if (ghostCursor != null)
        {
            ghostCursor.position = trueCenterPos;
            ghostCursor.localScale = new Vector3(currentConfig.size, currentConfig.size, 1f);

            if (ghostRenderer == null) ghostRenderer = ghostCursor.GetComponent<SpriteRenderer>();

            if (ghostRenderer != null)
            {
                ghostRenderer.sortingOrder = 9999;
                if (canPlaceHere)
                {
                    ghostRenderer.color = new Color(0.3f, 1f, 0.3f, 0.6f); 
                }
                else
                {
                    ghostRenderer.color = new Color(1f, 0.2f, 0.2f, 0.6f);
                }
            }
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            TryPlaceBuilding(originCell3D, currentConfig);
        }
    }

    private bool CheckAreaAvailable(Vector2Int origin, int size)
    {
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                Vector2Int checkCell = new Vector2Int(origin.x + x, origin.y + y);
                if (buildingGridMatrix.TryGetValue(checkCell, out ushort buildingID))
                {
                    if (buildingID != 0)
                    {
                        return false;
                    }
                }
            }
        }
        return true;
    }

    void TryPlaceBuilding(Vector3Int originCell3D, BuildingConfig config)
    {
        Vector2Int checkOrigin = new Vector2Int(originCell3D.x, originCell3D.y);

        // Kiểm tra an toàn trước khi đặt
        if (!CheckAreaAvailable(checkOrigin, config.size))
        {
            Debug.LogWarning("[Quy Hoạch] Vướng đất rồi, không thể xây đè!");
            return;
        }

        ref GlobalSystemData globalData = ref globalSystemManager.GetGlobalDataRef();

        if (globalData.stockWood < config.costWood || globalData.treasuryGold < config.costGold)
        {
            Debug.LogWarning($"[Tài Chính] Nghèo! Cần {config.costWood} Gỗ và {config.costGold} Vàng.");
            return;
        }

        globalData.stockWood -= config.costWood;
        globalData.treasuryGold -= config.costGold;
        GameEvents.Post(EventID.ResourcesChanged);

        BuildingData newData = new BuildingData
        {
            buildingID = (ushort)UnityEngine.Random.Range(100, 9999),
            buildingType = config.type,
            coordX = (ushort)checkOrigin.x,
            coordY = (ushort)checkOrigin.y,
            sizeFootprint = (byte)config.size,
            buildingState = BuildingState.Constructing
        };

        int dataIndex = buildingManager.AddBuilding(newData);

        if (dataIndex != -1)
        {
            for (int x = 0; x < config.size; x++)
            {
                for (int y = 0; y < config.size; y++)
                {
                    Vector2Int occupiedCell2D = new Vector2Int(checkOrigin.x + x, checkOrigin.y + y);
                    buildingGridMatrix[occupiedCell2D] = newData.buildingID;
                }
            }

            if (config.visualPrefab != null)
            {
                Vector3 basePos = isometricGrid.GetCellCenterWorld(originCell3D);
                Vector3Int topCell3D = originCell3D + new Vector3Int(config.size - 1, config.size - 1, 0);
                Vector3 topPos = isometricGrid.GetCellCenterWorld(topCell3D);

                Vector3 trueCenterPos = (basePos + topPos) / 2f;
                trueCenterPos.z = 0f;

                GameObject spawnedBuilding = Instantiate(config.visualPrefab, trueCenterPos, Quaternion.identity);
                spawnedBuilding.transform.localScale = new Vector3(config.size, config.size, 1f);
            }

            Debug.Log($"[Xây Dựng] Đã cook {config.displayName} thành công!");
        }
    }
}