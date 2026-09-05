using System;
using UnityEngine;

public class BuildingManager : MonoBehaviour
{
    public const int MAX_BUILDINGS = 4096;

    [Header("Dữ Liệu Công Trình")]
    public BuildingData[] allBuildings = new BuildingData[MAX_BUILDINGS];
    public int activeCount = 0;

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

    // Hook tái dựng Prefab nhà cửa sau khi nạp file save
    public void RebuildVisualCity()
    {
        Debug.Log($"[BuildingManager] Đã tái tạo {activeCount} công trình lên lưới Grid.");
    }
}