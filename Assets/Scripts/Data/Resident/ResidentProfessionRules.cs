public static class ResidentProfessionRules
{
    public const ProfessionType SampleProfession = ProfessionType.Farmer;

    public static ProfessionType GetInitialProfession(byte age)
    {
        if (age < 18 || age >= 65)
            return ProfessionType.None;

        return ProfessionType.Farmer;
    }

    public static bool CanReceiveProfession(in ResidentData resident)
    {
        if (!resident.isAlive)
            return false;

        if (resident.professionType != ProfessionType.None)
            return false;

        if (resident.GetAgeGroup() != AgeGroupType.Adult)
            return false;

        if (resident.healthStatus == HealthStatus.ActiveInfected)
            return false;

        return true;
    }

    public static bool TryAssignFarmer(ref ResidentData resident)
    {
        if (!CanReceiveProfession(in resident))
            return false;

        resident.professionType = ProfessionType.Farmer;
        return true;
    }

    public static bool CountsTowardFarmerLabor(in ResidentData resident) =>
        resident.professionType == ProfessionType.Farmer
        && ResidentAssignmentRules.CanWork(in resident);

    public static int GetFarmerCapacity(in BuildingData building)
    {
        if (building.buildingType != BuildingType.Farm)
            return 0;

        if (!ResidentAssignmentRules.IsBuildingAcceptingResidents(in building))
            return 0;

        return building.maxWorkers;
    }
}
