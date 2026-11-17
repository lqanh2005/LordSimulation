using System.Collections.Generic;
using System.Text;

/// <summary>ID công nghệ. Giá trị = vị trí bit trong mask đã nghiên cứu (tối đa 63). Không đổi số khi đã có save.</summary>
public enum ResearchID : byte
{
    None = 0,

    // Nông nghiệp
    CropRotation = 1,
    Herbalism = 2,
    Irrigation = 3,
    IronTools = 4,

    // Công nghiệp
    Forestry = 10,
    DeepMining = 11,
    Metallurgy = 12,
    CoalLogistics = 13,

    // Xã hội & Y tế
    Sanitation = 20,
    Medicine = 21,
    Pharmacy = 22,
    Quarantine = 23,
    Masonry = 24,
    Insulation = 25,
    Administration = 26,
    Thermometer = 27        // GDD Tier 1: đo thân nhiệt người nhập cư tại Cổng
}

public enum ResearchBranch : byte { Agriculture = 0, Industry = 1, Society = 2 }

public enum ResearchEffectType : byte
{
    None = 0,
    UnlockBuilding = 1,      // param0 = BuildingType
    ProductionBonus = 2,     // param0 = BuildingType, param1 = % cộng thêm
    DiseaseReduction = 3,    // param1 = giảm áp lực dịch bệnh mỗi tháng
    WarmPeople = 4,          // param1 = số người được sưởi miễn phí mùa đông
    GoldPerMonth = 5,        // param1 = vàng mỗi tháng
    ResearchSpeed = 6        // param1 = % điểm nghiên cứu cộng thêm
}

public readonly struct ResearchEffect
{
    public readonly ResearchEffectType type;
    public readonly byte param0;
    public readonly float param1;

    public ResearchEffect(ResearchEffectType type, byte param0, float param1)
    {
        this.type = type;
        this.param0 = param0;
        this.param1 = param1;
    }

    public static ResearchEffect Unlock(BuildingType building) =>
        new ResearchEffect(ResearchEffectType.UnlockBuilding, (byte)building, 0f);

    public static ResearchEffect Production(BuildingType building, float percent) =>
        new ResearchEffect(ResearchEffectType.ProductionBonus, (byte)building, percent);

    public static ResearchEffect Disease(float amount) =>
        new ResearchEffect(ResearchEffectType.DiseaseReduction, 0, amount);

    public static ResearchEffect Warm(int people) =>
        new ResearchEffect(ResearchEffectType.WarmPeople, 0, people);

    public static ResearchEffect Gold(int amount) =>
        new ResearchEffect(ResearchEffectType.GoldPerMonth, 0, amount);

    public static ResearchEffect Speed(float percent) =>
        new ResearchEffect(ResearchEffectType.ResearchSpeed, 0, percent);
}

public sealed class ResearchNode
{
    public readonly ResearchID id;
    public readonly ResearchBranch branch;
    public readonly string displayName;
    public readonly string description;
    public readonly int cost;          // Điểm nghiên cứu cần để hoàn thành
    public readonly int tier;          // Cột trên cây (0 = gốc)
    public readonly float row;         // Hàng trên cây (cho phép 0.5 để căn giữa)
    public readonly ResearchID[] prerequisites;
    public readonly ResearchEffect[] effects;

    public ResearchNode(
        ResearchID id, ResearchBranch branch, string displayName, string description,
        int cost, int tier, float row, ResearchID[] prerequisites, params ResearchEffect[] effects)
    {
        this.id = id;
        this.branch = branch;
        this.displayName = displayName;
        this.description = description;
        this.cost = cost;
        this.tier = tier;
        this.row = row;
        this.prerequisites = prerequisites ?? System.Array.Empty<ResearchID>();
        this.effects = effects ?? System.Array.Empty<ResearchEffect>();
    }
}

/// <summary>Định nghĩa cây nghiên cứu (dữ liệu tĩnh, không lưu vào save).</summary>
public static class ResearchCatalog
{
    private static readonly ResearchID[] NoPrereq = System.Array.Empty<ResearchID>();

    public static readonly ResearchNode[] All =
    {
        // ===== NÔNG NGHIỆP (hàng 0-1) =====
        new ResearchNode(ResearchID.CropRotation, ResearchBranch.Agriculture, "Luân canh",
            "Xoay vòng cây trồng giúp đất màu mỡ hơn.", 15, 0, 0.5f, NoPrereq,
            ResearchEffect.Production(BuildingType.Farm, 15f)),
        new ResearchNode(ResearchID.Herbalism, ResearchBranch.Agriculture, "Thảo dược",
            "Trồng cây thuốc để tự chủ nguồn thuốc men.", 25, 1, 0f, new[] { ResearchID.CropRotation },
            ResearchEffect.Unlock(BuildingType.HerbFarm)),
        new ResearchNode(ResearchID.Irrigation, ResearchBranch.Agriculture, "Thủy lợi",
            "Kênh dẫn nước tưới ổn định cho ruộng vườn.", 35, 1, 1f, new[] { ResearchID.CropRotation },
            ResearchEffect.Production(BuildingType.Farm, 20f),
            ResearchEffect.Production(BuildingType.HerbFarm, 15f)),
        new ResearchNode(ResearchID.IronTools, ResearchBranch.Agriculture, "Nông cụ sắt",
            "Cày, rìu bằng sắt giúp nông dân và tiều phu làm nhanh hơn.", 60, 3, 1f,
            new[] { ResearchID.Irrigation, ResearchID.Metallurgy },
            ResearchEffect.Production(BuildingType.Farm, 15f),
            ResearchEffect.Production(BuildingType.LumberCamp, 15f)),

        // ===== CÔNG NGHIỆP (hàng 2-3) =====
        new ResearchNode(ResearchID.Forestry, ResearchBranch.Industry, "Lâm nghiệp",
            "Kỹ thuật đốn hạ và vận chuyển gỗ hiệu quả.", 15, 0, 2.5f, NoPrereq,
            ResearchEffect.Production(BuildingType.LumberCamp, 20f)),
        new ResearchNode(ResearchID.DeepMining, ResearchBranch.Industry, "Khai mỏ sâu",
            "Đào sâu hơn để lấy thêm quặng sắt và than.", 30, 1, 2f, new[] { ResearchID.Forestry },
            ResearchEffect.Production(BuildingType.Mine, 20f)),
        new ResearchNode(ResearchID.CoalLogistics, ResearchBranch.Industry, "Kho vận than",
            "Tổ chức kho bãi để tích trữ và phân phối than.", 30, 1, 3f, new[] { ResearchID.Forestry },
            ResearchEffect.Unlock(BuildingType.CoalStore)),
        new ResearchNode(ResearchID.Metallurgy, ResearchBranch.Industry, "Luyện kim",
            "Nung chảy quặng, rèn sắt thành phẩm.", 45, 2, 2f, new[] { ResearchID.DeepMining },
            ResearchEffect.Unlock(BuildingType.Forge),
            ResearchEffect.Production(BuildingType.Mine, 10f)),

        // ===== XÃ HỘI & Y TẾ (hàng 4-6) =====
        new ResearchNode(ResearchID.Thermometer, ResearchBranch.Society, "Nhiệt kế",
            "Đo thân nhiệt người xin vào tại Cổng. Mở điều kiện \"Thân nhiệt\" trong Sắc lệnh.", 12, 0, 6f, NoPrereq),
        new ResearchNode(ResearchID.Sanitation, ResearchBranch.Society, "Vệ sinh",
            "Đun nước sôi, dọn rác thải để hạn chế dịch bệnh.", 20, 0, 4.5f, NoPrereq,
            ResearchEffect.Disease(0.006f)),
        new ResearchNode(ResearchID.Medicine, ResearchBranch.Society, "Y học",
            "Đào tạo thầy thuốc chữa bệnh cho dân.", 35, 1, 4f, new[] { ResearchID.Sanitation },
            ResearchEffect.Unlock(BuildingType.Clinic)),
        new ResearchNode(ResearchID.Quarantine, ResearchBranch.Society, "Kiểm dịch",
            "Cách ly người bệnh để chặn lây lan.", 40, 1, 5f, new[] { ResearchID.Sanitation },
            ResearchEffect.Unlock(BuildingType.QuarantineWard),
            ResearchEffect.Disease(0.006f)),
        new ResearchNode(ResearchID.Pharmacy, ResearchBranch.Society, "Bào chế",
            "Xưởng lò bào chế thảo dược thành thuốc.", 50, 2, 4f,
            new[] { ResearchID.Medicine, ResearchID.Herbalism },
            ResearchEffect.Unlock(BuildingType.Furnace),
            ResearchEffect.Production(BuildingType.HerbFarm, 20f)),
        new ResearchNode(ResearchID.Masonry, ResearchBranch.Society, "Xây gạch",
            "Nhà gạch rộng rãi, chứa được nhiều người hơn.", 30, 1, 6f, new[] { ResearchID.Forestry },
            ResearchEffect.Unlock(BuildingType.LargeHouse)),
        new ResearchNode(ResearchID.Insulation, ResearchBranch.Society, "Cách nhiệt",
            "Trát tường, chèn khe giúp dân đỡ rét mùa đông.", 40, 2, 6f, new[] { ResearchID.Masonry },
            ResearchEffect.Warm(10)),
        new ResearchNode(ResearchID.Administration, ResearchBranch.Society, "Hành chính",
            "Bộ máy quản lý thu thuế và tổ chức học thuật.", 70, 3, 5f,
            new[] { ResearchID.Insulation, ResearchID.Pharmacy },
            ResearchEffect.Gold(20),
            ResearchEffect.Speed(25f)),
    };

    private static Dictionary<ResearchID, ResearchNode> byId;
    private static Dictionary<BuildingType, ResearchID> unlockByBuilding;

    public static int MaxTier
    {
        get
        {
            int max = 0;
            for (int i = 0; i < All.Length; i++)
                if (All[i].tier > max) max = All[i].tier;
            return max;
        }
    }

    public static float MaxRow
    {
        get
        {
            float max = 0f;
            for (int i = 0; i < All.Length; i++)
                if (All[i].row > max) max = All[i].row;
            return max;
        }
    }

    public static ResearchNode Get(ResearchID id)
    {
        EnsureLookups();
        return byId.TryGetValue(id, out ResearchNode node) ? node : null;
    }

    /// <summary>Công nghệ cần nghiên cứu để được xây công trình này (None = mở sẵn).</summary>
    public static ResearchID GetRequiredResearch(BuildingType type)
    {
        EnsureLookups();
        return unlockByBuilding.TryGetValue(type, out ResearchID id) ? id : ResearchID.None;
    }

    public static string GetBranchName(ResearchBranch branch) => branch switch
    {
        ResearchBranch.Agriculture => "Nông nghiệp",
        ResearchBranch.Industry => "Công nghiệp",
        ResearchBranch.Society => "Xã hội & Y tế",
        _ => branch.ToString()
    };

    public static string GetBuildingName(BuildingType type) => type switch
    {
        BuildingType.Tent => "Lều tạm",
        BuildingType.House => "Nhà dân",
        BuildingType.LargeHouse => "Nhà lớn",
        BuildingType.LumberCamp => "Trại gỗ",
        BuildingType.Farm => "Nông trại",
        BuildingType.Mine => "Mỏ",
        BuildingType.Furnace => "Xưởng lò",
        BuildingType.Forge => "Lò rèn",
        BuildingType.Clinic => "Phòng khám",
        BuildingType.School => "Trường học",
        BuildingType.GuardPost => "Trạm gác",
        BuildingType.QuarantineWard => "Khu cách ly",
        BuildingType.CityHall => "Tòa thị chính",
        BuildingType.WaterWell => "Giếng nước",
        BuildingType.CoalStore => "Kho than",
        BuildingType.Campfire => "Lửa trại",
        BuildingType.CoalCart => "Xe goòng",
        BuildingType.HerbFarm => "Vườn thuốc",
        BuildingType.Road => "Đường đất",
        _ => type.ToString()
    };

    public static string DescribeEffect(in ResearchEffect effect)
    {
        switch (effect.type)
        {
            case ResearchEffectType.UnlockBuilding:
                return $"Mở khóa: {GetBuildingName((BuildingType)effect.param0)}";
            case ResearchEffectType.ProductionBonus:
                return $"{GetBuildingName((BuildingType)effect.param0)}: +{effect.param1:0}% sản lượng";
            case ResearchEffectType.DiseaseReduction:
                return $"Giảm áp lực dịch bệnh {effect.param1 * 100f:0.#}%/tháng";
            case ResearchEffectType.WarmPeople:
                return $"Sưởi ấm miễn phí thêm {effect.param1:0} người mùa đông";
            case ResearchEffectType.GoldPerMonth:
                return $"+{effect.param1:0} vàng mỗi tháng";
            case ResearchEffectType.ResearchSpeed:
                return $"+{effect.param1:0}% điểm nghiên cứu";
            default:
                return string.Empty;
        }
    }

    public static string DescribeEffects(ResearchNode node)
    {
        if (node == null || node.effects.Length == 0)
            return string.Empty;

        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < node.effects.Length; i++)
        {
            if (i > 0) sb.Append('\n');
            sb.Append("• ").Append(DescribeEffect(in node.effects[i]));
        }
        return sb.ToString();
    }

    private static void EnsureLookups()
    {
        if (byId != null)
            return;

        byId = new Dictionary<ResearchID, ResearchNode>(All.Length);
        unlockByBuilding = new Dictionary<BuildingType, ResearchID>();
        for (int i = 0; i < All.Length; i++)
        {
            ResearchNode node = All[i];
            byId[node.id] = node;
            for (int e = 0; e < node.effects.Length; e++)
            {
                if (node.effects[e].type == ResearchEffectType.UnlockBuilding)
                    unlockByBuilding[(BuildingType)node.effects[e].param0] = node.id;
            }
        }
    }
}
