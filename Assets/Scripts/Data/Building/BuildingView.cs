using UnityEngine;

public class BuildingView : MonoBehaviour
{
    private SpriteRenderer[] renderers;
    private Color[] baseColors;
    private int[] orderOffsets;

    public ushort BuildingId { get; private set; }
    public BuildingType Type { get; private set; }
    public Vector2Int Origin { get; private set; }
    public Vector2Int Footprint { get; private set; }

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

    public void Init(ushort buildingId, BuildingType type, Vector2Int origin, Vector2Int footprint, string sortingLayerName, int sortingOrder)
    {
        BuildingId = buildingId;
        Type = type;
        Origin = origin;
        Footprint = footprint;
        gameObject.name = $"{type}_{buildingId}_{origin.x}_{origin.y}";

        SetSorting(sortingLayerName, sortingOrder);
    }

    public void SetSorting(string sortingLayerName, int sortingOrder)
    {
        SpriteRenderer[] list = Renderers;
        if (orderOffsets == null || orderOffsets.Length != list.Length)
        {
            // sortingOrder trong prefab = offset tương đối (vd: ruộng nằm trên khung Farm)
            orderOffsets = new int[list.Length];
            for (int i = 0; i < list.Length; i++)
            {
                orderOffsets[i] = list[i].sortingOrder;
            }
        }

        for (int i = 0; i < list.Length; i++)
        {
            list[i].sortingLayerName = sortingLayerName;
            list[i].sortingOrder = sortingOrder + orderOffsets[i];
        }
    }

    public void SetColor(Color color)
    {
        foreach (SpriteRenderer sr in Renderers)
        {
            sr.color = color;
        }
    }

    public void SetTint(Color tint)
    {
        SpriteRenderer[] list = Renderers;
        if (baseColors == null || baseColors.Length != list.Length)
        {
            baseColors = new Color[list.Length];
            for (int i = 0; i < list.Length; i++)
            {
                baseColors[i] = list[i].color;
            }
        }

        for (int i = 0; i < list.Length; i++)
        {
            list[i].color = baseColors[i] * tint;
        }
    }
}
