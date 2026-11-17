/// <summary>Mức độ của trạng thái công trình (để tô màu và quyết định có cảnh báo không).</summary>
public enum BuildingStatusLevel : byte
{
    Ok = 0,         // Đang chạy bình thường
    Limited = 1,    // Chạy nhưng bị giảm (mùa đông, thiếu một phần người…)
    Stopped = 2     // Không hoạt động
}

/// <summary>
/// GDD 7.2: "Nếu building không hoạt động: UI phải hiển thị trực tiếp nguyên nhân"
/// (vd. "Not Working — No Workers", "Not Working — Missing Coal"). Logic thuần, dùng chung cho popup và cảnh báo.
/// </summary>
public static class BuildingStatusRules
{
    /// <summary>Có cho phép người chơi Tạm dừng / Chạy lại không (nhà ở và Tòa thị chính thì không).</summary>
    public static bool CanToggle(BuildingType type) =>
        ResidentAssignmentRules.IsWorkplaceBuilding(type) && type != BuildingType.CityHall;

    public static BuildingStatusLevel Evaluate(
        in BuildingData b,
        in GlobalSystemData global,
        bool roadConnected,
        out string reason)
    {
        if (BuildingRules.IsUnderConstruction(in b))
        {
            int pct = UnityEngine.Mathf.RoundToInt(UnityEngine.Mathf.Clamp01(b.constructionProgress) * 100f);
            reason = $"Đang xây ({pct}%)";
            return BuildingStatusLevel.Limited;
        }

        if (!b.isOperational)
        {
            reason = "Đã tạm dừng (bấm \"Chạy lại\" để hoạt động)";
            return BuildingStatusLevel.Stopped;
        }

        if (!roadConnected)
        {
            reason = "Không hoạt động — chưa nối đường tới Tòa thị chính / Kho";
            return BuildingStatusLevel.Stopped;
        }

        BuildingType type = b.buildingType;

        if (FarmCropRules.HasCropSlot(type) && b.cropType == CropType.None)
        {
            reason = "Không hoạt động — ruộng trống, chưa chọn cây trồng";
            return BuildingStatusLevel.Stopped;
        }

        if (b.maxWorkers > 0 && b.currentWorkers == 0)
        {
            ProfessionType need = ResidentAssignmentRules.GetProfessionForWorkplace(type);
            string who = need != ProfessionType.None ? ResidentInfoPopup.GetProfessionName(need) : "công nhân";
            reason = $"Không hoạt động — không có {who.ToLowerInvariant()}";
            return BuildingStatusLevel.Stopped;
        }

        switch (type)
        {
            case BuildingType.Clinic:
            case BuildingType.QuarantineWard:
                if (global.stockMedicine <= 0)
                {
                    reason = "Thiếu thuốc men — chữa bệnh kém hiệu quả";
                    return BuildingStatusLevel.Limited;
                }
                break;
        }

        if (ResidentAssignmentRules.IsHousingBuilding(type))
        {
            if (b.currentOccupancy == 0)
            {
                reason = "Chưa có ai ở";
                return BuildingStatusLevel.Limited;
            }
            if (global.CurrentSeason == SeasonType.Winter && global.stockCoal <= 0)
            {
                reason = "Thiếu than — người ở đây đang bị lạnh";
                return BuildingStatusLevel.Stopped;
            }
        }

        if (ResidentProductionRules.IsProductionBuilding(type)
            && ResidentProductionRules.GetSeasonMultiplier(type, global.CurrentSeason) < 0.5f)
        {
            reason = "Mùa đông — sản lượng giảm mạnh";
            return BuildingStatusLevel.Limited;
        }

        if (b.maxWorkers > 0 && b.currentWorkers < b.maxWorkers)
        {
            reason = $"Hoạt động — thiếu người ({b.currentWorkers}/{b.maxWorkers})";
            return BuildingStatusLevel.Limited;
        }

        reason = "Hoạt động";
        return BuildingStatusLevel.Ok;
    }

    /// <summary>Mô tả sản lượng tháng trước của công trình sản xuất (rỗng nếu không sản xuất).</summary>
    public static string DescribeOutput(in BuildingData b)
    {
        if (!ResidentProductionRules.IsProductionBuilding(b.buildingType))
            return string.Empty;
        if (!ResidentProductionRules.TryGetYield(in b, out ResourceType primary, out _, out ResourceType secondary, out _))
            return string.Empty;

        string text = $"Sản lượng tháng trước: {b.outputStored} {MerchantManager.GetResourceName(primary).ToLowerInvariant()}";
        if (secondary != ResourceType.None)
            text += $" (+ {MerchantManager.GetResourceName(secondary).ToLowerInvariant()})";
        return text;
    }
}
