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
        if (_cachedCount == index)
        {
            _idToIndex[newBuilding.buildingID] = index + 1;
            _cachedCount = activeCount;
        }
        return index;
    }

    public ref BuildingData GetBuildingRef(int index)
    {
        if ((uint)index >= (uint)activeCount)
            throw new ArgumentOutOfRangeException(nameof(index));

        return ref allBuildings[index];
    }

    /// <summary>
    /// Xoá công trình khỏi mảng (dồn mảng, giữ thứ tự). Trả về false nếu không tìm thấy.
    /// Mọi tham chiếu tới công trình đều qua buildingID nên dồn index là an toàn.
    /// </summary>
    public bool RemoveBuilding(ushort buildingId, out BuildingData removed)
    {
        removed = default;
        int index = FindBuildingIndexById(buildingId);
        if (index < 0)
            return false;

        removed = allBuildings[index];
        int tail = activeCount - index - 1;
        if (tail > 0)
            Array.Copy(allBuildings, index + 1, allBuildings, index, tail);

        activeCount--;
        allBuildings[activeCount] = default;
        _cachedCount = -1; // index phía sau đã dịch → dựng lại khi tra lần tới
        return true;
    }

    // ===== Cache id → index (O(1)) =====
    // Lưu index+1 theo buildingID (0 = không có). Tự dựng lại khi activeCount đổi hoặc gặp entry lệch,
    // nên vẫn đúng cả khi save loader ghi thẳng vào allBuildings/activeCount.
    private readonly int[] _idToIndex = new int[ushort.MaxValue + 1];
    private int _cachedCount = -1;

    public int FindBuildingIndexById(ushort buildingId)
    {
        if (_cachedCount != activeCount)
            RebuildIdCache();

        int index = _idToIndex[buildingId] - 1;
        if (index >= 0 && index < activeCount && allBuildings[index].buildingID == buildingId)
            return index;

        if (index < 0)
            return -1;

        // Entry lệch (mảng bị sửa từ ngoài) → dựng lại 1 lần rồi tra tiếp
        RebuildIdCache();
        index = _idToIndex[buildingId] - 1;
        return index >= 0 && index < activeCount && allBuildings[index].buildingID == buildingId ? index : -1;
    }

    /// <summary>Gọi khi sửa allBuildings trực tiếp mà không đổi activeCount (vd: đổi buildingID).</summary>
    public void InvalidateIdCache()
    {
        _cachedCount = -1;
    }

    private void RebuildIdCache()
    {
        Array.Clear(_idToIndex, 0, _idToIndex.Length);
        for (int i = 0; i < activeCount; i++)
            _idToIndex[allBuildings[i].buildingID] = i + 1;
        _cachedCount = activeCount;
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
            RoadNetwork.MarkDirty();   // Tòa thị chính xây xong mới thành trung tâm mạng đường
            GameEvents.Post(EventID.BuildingCompleted, building.buildingID);
        }

        return completed;
    }

    public void ProcessMonthlyCrops(SeasonType season)
    {
        for (int i = 0; i < activeCount; i++)
            FarmCropRules.TickMonthly(ref allBuildings[i], season);

        if (placementManager == null)
            placementManager = FindFirstObjectByType<PlacementManager>();
        if (placementManager != null)
            placementManager.RefreshFarmVisuals();
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