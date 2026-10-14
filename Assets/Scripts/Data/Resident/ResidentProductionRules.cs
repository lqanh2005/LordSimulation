public static class ResidentProductionRules
{
    public const float FarmFoodPerWorker = 6f;
    public const float LumberWoodPerWorker = 4f;
    public const float MineIronPerWorker = 2f;
    public const float MineCoalPerWorker = 3f;
    public const float FurnaceMedicinePerWorker = 1.5f;
    public const float IncubatingWorkPenalty = 0.8f;

    public static bool IsProducer(ProfessionType profession) =>
        profession == ProfessionType.Farmer
        || profession == ProfessionType.Miner
        || profession == ProfessionType.Lumberjack
        || profession == ProfessionType.Craftsman;

    public static bool IsWorkingProducer(in ResidentData resident) =>
        IsProducer(resident.professionType)
        && ResidentAssignmentRules.CanWork(in resident)
        && resident.assignedWorkID >= 0;

    public static float GetWorkerPower(in ResidentData resident)
    {
        float power = resident.WorkMultiplier;
        if (resident.healthStatus == HealthStatus.Incubating)
            power *= IncubatingWorkPenalty;
        return power;
    }

    public static bool IsProductionBuilding(BuildingType type) =>
        type == BuildingType.Farm
        || type == BuildingType.LumberCamp
        || type == BuildingType.Mine
        || type == BuildingType.Furnace;

    public static bool TryGetYield(
        BuildingType type,
        out ResourceType primary,
        out float primaryPerWorker,
        out ResourceType secondary,
        out float secondaryPerWorker)
    {
        secondary = ResourceType.None;
        secondaryPerWorker = 0f;

        switch (type)
        {
            case BuildingType.Farm:
                primary = ResourceType.Food;
                primaryPerWorker = FarmFoodPerWorker;
                return true;
            case BuildingType.LumberCamp:
                primary = ResourceType.Wood;
                primaryPerWorker = LumberWoodPerWorker;
                return true;
            case BuildingType.Mine:
                primary = ResourceType.Iron;
                primaryPerWorker = MineIronPerWorker;
                secondary = ResourceType.Coal;
                secondaryPerWorker = MineCoalPerWorker;
                return true;
            case BuildingType.Furnace:
                primary = ResourceType.Medicine;
                primaryPerWorker = FurnaceMedicinePerWorker;
                return true;
            default:
                primary = ResourceType.None;
                primaryPerWorker = 0f;
                return false;
        }
    }

    public static float GetSeasonMultiplier(BuildingType type, SeasonType season)
    {
        if (type != BuildingType.Farm)
            return 1f;

        return season switch
        {
            SeasonType.Spring => 1.0f,
            SeasonType.Summer => 1.2f,
            SeasonType.Autumn => 1.1f,
            SeasonType.Winter => 0.3f,
            _ => 1f
        };
    }
}
