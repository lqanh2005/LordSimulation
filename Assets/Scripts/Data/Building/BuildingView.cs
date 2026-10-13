using UnityEngine;

public class BuildingView : MonoBehaviour
{
    private SpriteRenderer[] renderers;

    public ushort BuildingId { get; private set; }
    public BuildingType Type { get; private set; }
    public Vector2Int Origin { get; private set; }
    public int Size { get; private set; }

    public SpriteRenderer[] Renderers
    {
        get
        {
            if (renderers == null)
            {
                renderers = GetComponentsInChildren<SpriteRenderer>(true);
            }

            return renderers;
        }
    }

    public void Init(ushort buildingId, BuildingType type, Vector2Int origin, int size, string sortingLayerName, int sortingOrder)
    {
        BuildingId = buildingId;
        Type = type;
        Origin = origin;
        Size = size;
        gameObject.name = $"{type}_{buildingId}_{origin.x}_{origin.y}";

        SetSorting(sortingLayerName, sortingOrder);
    }

    public void SetSorting(string sortingLayerName, int sortingOrder)
    {
        foreach (SpriteRenderer sr in Renderers)
        {
            sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder = sortingOrder;
        }
    }

    public void SetColor(Color color)
    {
        foreach (SpriteRenderer sr in Renderers)
        {
            sr.color = color;
        }
    }
}
