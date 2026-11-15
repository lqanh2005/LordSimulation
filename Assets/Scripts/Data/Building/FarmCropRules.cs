/// <summary>
/// Ruộng của Farm: đất trống đổi theo mùa; khi trồng Lúa/Thuốc thì lớn dần qua 4 giai đoạn,
/// chín xong tự thu hoạch và gieo lại vụ mới cùng loại.
/// </summary>
public static class FarmCropRules
{
    public const int StageCount = 4;

    public static bool HasCropSlot(BuildingType type) => type == BuildingType.Farm;

    public static void Plant(ref BuildingData building, CropType crop)
    {
        if (!HasCropSlot(building.buildingType))
            return;

        building.cropType = crop;
        building.cropStage = 0;
    }

    /// <summary>Lớn thêm 1 giai đoạn mỗi tháng (mùa đông ngừng lớn). Trả về true nếu hình ruộng cần đổi.</summary>
    public static bool TickMonthly(ref BuildingData building, SeasonType season)
    {
        if (!HasCropSlot(building.buildingType)
            || building.cropType == CropType.None
            || BuildingRules.IsUnderConstruction(in building)
            || season == SeasonType.Winter)
            return false;

        building.cropStage = building.cropStage + 1 >= StageCount
            ? (byte)0 // thu hoạch -> gieo lại
            : (byte)(building.cropStage + 1);
        return true;
    }

    public static bool TryGetYield(CropType crop, out ResourceType resource, out float perWorker)
    {
        switch (crop)
        {
            case CropType.Rice:
                resource = ResourceType.Food;
                perWorker = ResidentProductionRules.FarmFoodPerWorker;
                return true;
            case CropType.Drug:
                resource = ResourceType.Medicine;
                perWorker = ResidentProductionRules.HerbFarmMedicinePerWorker;
                return true;
            default:
                resource = ResourceType.None;
                perWorker = 0f;
                return false;
        }
    }

    /// <summary>0 = đất ẩm (Xuân/Thu), 1 = đất khô (Hè), 2 = tuyết (Đông).</summary>
    public static int GetDirtIndex(SeasonType season) => season switch
    {
        SeasonType.Summer => 1,
        SeasonType.Winter => 2,
        _ => 0
    };

    public static string GetCropName(CropType crop) => crop switch
    {
        CropType.Rice => "Lúa",
        CropType.Drug => "Cây thuốc",
        _ => "Đất trống"
    };
}
