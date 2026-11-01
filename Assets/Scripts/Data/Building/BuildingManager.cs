using System;
using UnityEngine;

public class BuildingManager : MonoBehaviour
{
    public const int MAX_BUILDINGS = 4096;

    [Header("Dữ Liệu Công Trình")]
    public BuildingData[] allBuildings = new BuildingData[MAX_BUILDINGS];
    public int activeCount = 0;

    [Header("Hiển Thị")]
    public PlacementManager placementManager;

    public int AddBuilding(in BuildingData newBuilding)
    {
        if (activeCount >= MAX_BUILDINGS)
        {
            Debug.LogError("[BuildingManager] Bản đồ đã hết chỗ xây dựng!");
            return -1;
        }

        int index = activeCount;
        allBuildings[index] = newBuilding;
        allBuildings[index].isAllocated = true;
        activeCount++;
        return index;
    }

    public ref BuildingData GetBuildingRef(int index)
    {
        if ((uint)index >= (uint)activeCount)
            throw new ArgumentOutOfRangeException(nameof(index));

        return ref allBuildings[index];
    }

    public int FindBuildingIndexById(ushort buildingId)
    {
        for (int i = 0; i < activeCount; i++)
        {
            if (allBuildings[i].buildingID == buildingId)
                return i;
        }

        return -1;
    }

    public int ProcessMonthlyConstruction()
    {
        if (placementManager == null)
        {
            placementManager = FindFirstObjectByType<PlacementManager>();
        }

        BuildingDatabase database = placementManager != null ? placementManager.database : null;
        int completed = 0;

        for (int i = 0; i < activeCount; i++)
        {
            ref BuildingData building = ref allBuildings[i];

            if (building.buildingState == BuildingState.Active)
            {
                BuildingRules.EnsureCapacity(ref building);
                continue;
            }

            BuildingConfig config = database != null ? database.GetConfig(building.buildingType) : null;
            int months = BuildingRules.ResolveConstructionMonths(building.buildingType, config);
            if (!BuildingRules.TickConstruction(ref building, months))
                continue;

            completed++;
            GameEvents.Post(EventID.BuildingCompleted, building.buildingID);
        }

        return completed;
    }

    // Hook tái dựng Prefab nhà cửa sau khi nạp file save
    public void RebuildVisualCity()
    {
        if (placementManager == null)
        {
            placementManager = FindFirstObjectByType<PlacementManager>();
        }

        if (placementManager != null)
        {
            placementManager.RebuildVisuals();
        }


        Debug.Log($"[BuildingManager] Đã tái tạo {activeCount} công trình lên lưới Grid.");
    }
}