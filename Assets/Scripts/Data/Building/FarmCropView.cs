using UnityEngine;

/// <summary>Hiển thị ô ruộng trống trong khung Farm theo loại cây / giai đoạn / mùa.</summary>
public class FarmCropView : MonoBehaviour
{
    [SerializeField] private SpriteRenderer cropRenderer;

    [Tooltip("Đất trống: 0 = Xuân/Thu, 1 = Hè, 2 = Đông")]
    [SerializeField] private Sprite[] dirtBySeason = new Sprite[3];

    [Tooltip("Lúa theo giai đoạn 1..4")]
    [SerializeField] private Sprite[] riceStages = new Sprite[FarmCropRules.StageCount];

    [Tooltip("Cây thuốc theo giai đoạn 1..4")]
    [SerializeField] private Sprite[] drugStages = new Sprite[FarmCropRules.StageCount];

    public void Configure(SpriteRenderer renderer, Sprite[] dirt, Sprite[] rice, Sprite[] drug)
    {
        cropRenderer = renderer;
        dirtBySeason = dirt;
        riceStages = rice;
        drugStages = drug;
    }

    public void Apply(in BuildingData building, SeasonType season)
    {
        if (cropRenderer == null)
            return;

        bool showCrop = building.cropType != CropType.None && !BuildingRules.IsUnderConstruction(in building);
        Sprite sprite = showCrop
            ? Pick(building.cropType == CropType.Rice ? riceStages : drugStages, building.cropStage)
            : Pick(dirtBySeason, FarmCropRules.GetDirtIndex(season));

        if (sprite != null)
            cropRenderer.sprite = sprite;
    }

    private static Sprite Pick(Sprite[] list, int index)
    {
        if (list == null || list.Length == 0)
            return null;

        return list[Mathf.Clamp(index, 0, list.Length - 1)];
    }
}
