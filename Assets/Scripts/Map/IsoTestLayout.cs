using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Xếp các prefab art lên GridController để test kích thước/footprint.
/// Chạy sau GridController (map phải sinh xong trước).
/// </summary>
[DefaultExecutionOrder(100)]
public class IsoTestLayout : MonoBehaviour
{
    [Serializable]
    public class Entry
    {
        public IsoFootprint prefab;
        public Vector2Int origin;
        [Tooltip("Đặt chồng lên công trình khác (vd: ruộng trong Farm) - bỏ qua kiểm tra chiếm ô")]
        public bool overlay;
    }

    public GridController grid;
    public bool buildOnStart = true;
    public bool drawFootprintGizmos = true;
    public List<Entry> entries = new List<Entry>();

    private void Start()
    {
        if (buildOnStart)
        {
            Build();
        }
    }

    [ContextMenu("Build Layout")]
    public void Build()
    {
        if (grid == null)
        {
            grid = FindFirstObjectByType<GridController>();
        }

        if (grid == null || !grid.IsGenerated)
        {
            Debug.LogWarning("[IsoTestLayout] Grid chưa sinh map.");
            return;
        }

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }

        HashSet<Vector2Int> occupied = new HashSet<Vector2Int>();

        foreach (Entry entry in entries)
        {
            if (entry.prefab == null)
            {
                continue;
            }

            Vector2Int size = entry.prefab.size;
            if (!entry.overlay && !TryOccupy(occupied, entry.origin, size))
            {
                Debug.LogWarning($"[IsoTestLayout] {entry.prefab.name} {size.x}x{size.y} tại {entry.origin} bị chồng/ra ngoài map.");
                continue;
            }

            for (int x = 0; x < size.x; x++)
            {
                for (int y = 0; y < size.y; y++)
                {
                    grid.RemoveDecor(entry.origin.x + x, entry.origin.y + y, DecorLayer.Ground);
                    grid.RemoveDecor(entry.origin.x + x, entry.origin.y + y, DecorLayer.Object);
                }
            }

            IsoFootprint go = Instantiate(entry.prefab, grid.GetFootprintAnchorWorld(entry.origin), Quaternion.identity, transform);
            go.name = $"{entry.prefab.name}_{entry.origin.x}_{entry.origin.y}";

            int order = grid.GetFootprintSortingOrder(entry.origin, size);
            foreach (SpriteRenderer sr in go.GetComponentsInChildren<SpriteRenderer>(true))
            {
                sr.sortingLayerName = grid.SortingLayerName;
                sr.sortingOrder = order + sr.sortingOrder;
            }
        }
    }

    private bool TryOccupy(HashSet<Vector2Int> occupied, Vector2Int origin, Vector2Int size)
    {
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                Vector2Int c = new Vector2Int(origin.x + x, origin.y + y);
                if (!grid.IsInside(c.x, c.y) || occupied.Contains(c))
                {
                    return false;
                }
            }
        }

        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                occupied.Add(new Vector2Int(origin.x + x, origin.y + y));
            }
        }

        return true;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawFootprintGizmos || grid == null)
        {
            return;
        }

        Vector2 t = grid.TileSize;
        foreach (Entry entry in entries)
        {
            if (entry.prefab == null)
            {
                continue;
            }

            Gizmos.color = entry.overlay ? Color.yellow : Color.cyan;
            Vector2Int s = entry.prefab.size;
            Vector3 bottom = grid.GetFootprintAnchorWorld(entry.origin);
            Vector3 dx = new Vector3(t.x * 0.5f, t.y * 0.5f) * s.x;
            Vector3 dy = new Vector3(-t.x * 0.5f, t.y * 0.5f) * s.y;
            Gizmos.DrawLine(bottom, bottom + dx);
            Gizmos.DrawLine(bottom + dx, bottom + dx + dy);
            Gizmos.DrawLine(bottom + dx + dy, bottom + dy);
            Gizmos.DrawLine(bottom + dy, bottom);
        }
    }
}
