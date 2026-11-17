using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GDD 3.3.1 Road Connectivity: "Building phải có kết nối logistics tới Road Network / Storage để hoạt động."
///
/// Luật đang áp dụng:
/// - Trung tâm (hub) là Tòa thị chính — kho trung tâm của thành.
/// - Chỉ công trình SẢN XUẤT cần nối đường (Producer → Road → Storage). Nhà ở, y tế, trang trí không cần.
/// - Một công trình được nối nếu chạm trực tiếp Tòa thị chính, hoặc chạm một ô đường thuộc mạng đường
///   nối liền (4 hướng) tới Tòa thị chính.
/// - Trước khi có Tòa thị chính (khu định cư sơ khai) thì mọi công trình coi như đã nối.
/// Kết quả được cache, tính lại khi có công trình được đặt / phá / tải save (MarkDirty).
/// </summary>
public static class RoadNetwork
{
    private static readonly HashSet<ushort> Connected = new HashSet<ushort>();
    private static readonly HashSet<Vector2Int> RoadCells = new HashSet<Vector2Int>();
    private static readonly HashSet<Vector2Int> Reached = new HashSet<Vector2Int>();
    private static readonly Queue<Vector2Int> Frontier = new Queue<Vector2Int>();
    private static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    private static bool _dirty = true;
    private static bool _hasHub;
    private static int _lastCount = -1;

    /// <summary>Có thể tắt yêu cầu nối đường (vd. để test cân bằng).</summary>
    public static bool Enabled = true;

    public static bool HasHub => _hasHub;

    public static void MarkDirty() => _dirty = true;

    public static bool RequiresRoad(BuildingType type) => ResidentProductionRules.IsProductionBuilding(type);

    public static bool IsConnected(BuildingManager manager, ushort buildingId)
    {
        if (manager == null)
            return true;
        return IsConnected(manager.allBuildings, manager.activeCount, buildingId);
    }

    public static bool IsConnected(BuildingData[] buildings, int count, ushort buildingId)
    {
        if (!Enabled || buildings == null)
            return true;

        EnsureUpToDate(buildings, count);
        if (!_hasHub)
            return true;

        for (int i = 0; i < count; i++)
        {
            if (buildings[i].buildingID != buildingId)
                continue;
            return !RequiresRoad(buildings[i].buildingType) || Connected.Contains(buildingId);
        }
        return true;
    }

    private static void EnsureUpToDate(BuildingData[] buildings, int count)
    {
        if (!_dirty && count == _lastCount)
            return;

        _dirty = false;
        _lastCount = count;
        Rebuild(buildings, count);
    }

    private static void Rebuild(BuildingData[] buildings, int count)
    {
        Connected.Clear();
        RoadCells.Clear();
        Reached.Clear();
        Frontier.Clear();
        _hasHub = false;

        for (int i = 0; i < count; i++)
        {
            ref BuildingData b = ref buildings[i];
            if (!b.isAllocated || b.buildingState == BuildingState.Destroyed)
                continue;
            if (b.buildingType == BuildingType.Road)
                RoadCells.Add(new Vector2Int(b.coordX, b.coordY));
            else if (b.buildingType == BuildingType.CityHall)
                _hasHub = true;
        }

        if (!_hasHub)
            return;

        // 1. Mọi ô đường chạm Tòa thị chính là điểm xuất phát
        for (int i = 0; i < count; i++)
        {
            ref BuildingData b = ref buildings[i];
            if (b.buildingType != BuildingType.CityHall || !b.isAllocated)
                continue;

            ForEachRingCell(in b, cell =>
            {
                if (RoadCells.Contains(cell) && Reached.Add(cell))
                    Frontier.Enqueue(cell);
            });
        }

        // 2. Loang theo 4 hướng trên các ô đường
        while (Frontier.Count > 0)
        {
            Vector2Int cell = Frontier.Dequeue();
            foreach (Vector2Int d in Dirs)
            {
                Vector2Int next = cell + d;
                if (RoadCells.Contains(next) && Reached.Add(next))
                    Frontier.Enqueue(next);
            }
        }

        // 3. Công trình chạm ô đường đã nối, hoặc chạm thẳng Tòa thị chính
        for (int i = 0; i < count; i++)
        {
            ref BuildingData b = ref buildings[i];
            if (!b.isAllocated || !RequiresRoad(b.buildingType))
                continue;

            bool linked = false;
            ForEachRingCell(in b, cell =>
            {
                if (!linked && (Reached.Contains(cell) || IsHubCell(buildings, count, cell)))
                    linked = true;
            });
            if (linked)
                Connected.Add(b.buildingID);
        }
    }

    /// <summary>Các ô kề 4 hướng bao quanh footprint của công trình.</summary>
    private static void ForEachRingCell(in BuildingData b, System.Action<Vector2Int> visit)
    {
        Vector2Int origin = new Vector2Int(b.coordX, b.coordY);
        Vector2Int size = BuildingRules.GetFootprint(in b);
        for (int x = 0; x < size.x; x++)
        {
            visit(new Vector2Int(origin.x + x, origin.y - 1));
            visit(new Vector2Int(origin.x + x, origin.y + size.y));
        }
        for (int y = 0; y < size.y; y++)
        {
            visit(new Vector2Int(origin.x - 1, origin.y + y));
            visit(new Vector2Int(origin.x + size.x, origin.y + y));
        }
    }

    private static bool IsHubCell(BuildingData[] buildings, int count, Vector2Int cell)
    {
        for (int i = 0; i < count; i++)
        {
            ref BuildingData b = ref buildings[i];
            if (b.buildingType != BuildingType.CityHall || !b.isAllocated)
                continue;

            Vector2Int size = BuildingRules.GetFootprint(in b);
            if (cell.x >= b.coordX && cell.x < b.coordX + size.x && cell.y >= b.coordY && cell.y < b.coordY + size.y)
                return true;
        }
        return false;
    }
}
