using UnityEngine;

public static class BuildingRules
{
    private const float CompletionEpsilon = 0.0001f;

    public static int GetDefaultConstructionMonths(BuildingType type) => type switch
    {
        BuildingType.Tent => 0,
        BuildingType.Road => 0,
        BuildingType.Campfire => 0,
        BuildingType.CoalCart => 0,
        BuildingType.WaterWell => 1,
        BuildingType.CoalStore => 1,
        BuildingType.HerbFarm => 2,
        BuildingType.CityHall => 3,
        BuildingType.House => 1,
        BuildingType.LargeHouse => 2,
        BuildingType.Forge => 2,
        BuildingType.LumberCamp => 1,
        BuildingType.GuardPost => 1,
        BuildingType.Farm => 2,
        BuildingType.Mine => 2,
        BuildingType.Clinic => 2,
        BuildingType.School => 2,
        BuildingType.QuarantineWard => 2,
        BuildingType.Furnace => 3,
        _ => 1
    };

    public static byte GetDefaultMaxOccupancy(BuildingType type) => type switch
    {
        BuildingType.Tent => 3,
        BuildingType.House => 6,
        BuildingType.LargeHouse => 12,
        BuildingType.QuarantineWard => 8,
        BuildingType.Clinic => (byte)ResidentDiseaseSystem.InfirmaryBeds,   // GDD 7.4: Infirmary 8 giường
        BuildingType.School => 12,
        _ => 0
    };

    public static byte GetDefaultMaxWorkers(BuildingType type) => type switch
    {
        BuildingType.Farm => 6,
        BuildingType.HerbFarm => 4,
        BuildingType.CoalStore => 3,
        BuildingType.Mine => 6,
        BuildingType.LumberCamp => 4,
        BuildingType.Furnace => 4,
        BuildingType.Forge => 3,
        BuildingType.GuardPost => 4,
        BuildingType.Clinic => 3,
        BuildingType.School => 2,
        _ => 0
    };

    public static float GetDefaultInsulation(BuildingType type) => type switch
    {
        BuildingType.Tent => 0.2f,
        BuildingType.House => 0.6f,
        BuildingType.LargeHouse => 0.65f,
        BuildingType.QuarantineWard => 0.5f,
        BuildingType.Clinic => 0.5f,
        BuildingType.School => 0.5f,
        BuildingType.CityHall => 0.7f,
        _ => 0.3f
    };

    public static int ResolveConstructionMonths(BuildingType type, BuildingConfig config) =>
        config != null && config.constructionMonths > 0
            ? config.constructionMonths
            : GetDefaultConstructionMonths(type);

    public static BuildingData CreatePlaced(BuildingConfig config, ushort buildingId, Vector2Int origin)
    {
        BuildingData data = new BuildingData
        {
            buildingID = buildingId,
            buildingType = config.type,
            coordX = (ushort)origin.x,
            coordY = (ushort)origin.y,
            sizeFootprint = PackFootprint(config.Footprint),
            tierLevel = 1,
            buildingState = BuildingState.Constructing,
            constructionProgress = 0f,
            priorityLevel = 50,
            isOperational = false,
            maxOccupancy = config.maxOccupancy > 0
                ? (byte)Mathf.Min(config.maxOccupancy, byte.MaxValue)
                : GetDefaultMaxOccupancy(config.type),
            maxWorkers = config.maxWorkers > 0
                ? (byte)Mathf.Min(config.maxWorkers, byte.MaxValue)
                : GetDefaultMaxWorkers(config.type),
            efficiency = 1f,
            durability = 1f,
            insulationValue = GetDefaultInsulation(config.type)
        };

        if (ResolveConstructionMonths(config.type, config) <= 0)
            Complete(ref data);

        return data;
    }

    public static byte PackFootprint(Vector2Int size) =>
        (byte)(Mathf.Clamp(size.x, 1, 15) | (Mathf.Clamp(size.y, 1, 15) << 4));

    public static Vector2Int UnpackFootprint(byte packed)
    {
        int x = Mathf.Max(1, packed & 0x0F);
        int y = packed >> 4;
        return new Vector2Int(x, y == 0 ? x : y);
    }

    public static Vector2Int GetFootprint(in BuildingData building) => UnpackFootprint(building.sizeFootprint);

    public static bool IsUnderConstruction(in BuildingData building) =>
        building.isAllocated && building.buildingState == BuildingState.Constructing;

    public static bool TickConstruction(ref BuildingData building, int constructionMonths)
    {
        if (!IsUnderConstruction(in building))
            return false;

        float step = constructionMonths > 0 ? 1f / constructionMonths : 1f;
        building.constructionProgress = Mathf.Min(1f, building.constructionProgress + step);

        if (building.constructionProgress < 1f - CompletionEpsilon)
            return false;

        Complete(ref building);
        return true;
    }

    public static void Complete(ref BuildingData building)
    {
        building.buildingState = BuildingState.Active;
        building.constructionProgress = 1f;
        building.isOperational = true;
        if (building.efficiency <= 0f)
            building.efficiency = 1f;
        if (building.durability <= 0f)
            building.durability = 1f;
        EnsureCapacity(ref building);
    }

    public static void EnsureCapacity(ref BuildingData building)
    {
        if (building.maxOccupancy == 0)
            building.maxOccupancy = GetDefaultMaxOccupancy(building.buildingType);
        if (building.maxWorkers == 0)
            building.maxWorkers = GetDefaultMaxWorkers(building.buildingType);
    }
}
