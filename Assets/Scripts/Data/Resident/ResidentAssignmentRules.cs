using UnityEngine;

public static class ResidentAssignmentRules
{
    public const short UnassignedId = -1;

    public static bool IsHousingBuilding(BuildingType type) =>
        type == BuildingType.House || type == BuildingType.QuarantineWard;

    public static bool IsWorkplaceBuilding(BuildingType type) => type switch
    {
        BuildingType.Farm => true,
        BuildingType.Mine => true,
        BuildingType.Clinic => true,
        BuildingType.Furnace => true,
        BuildingType.School => true,
        BuildingType.GuardPost => true,
        BuildingType.LumberCamp => true,
        _ => false
    };

    public static BuildingType GetPreferredWorkplace(ProfessionType profession) => profession switch
    {
        ProfessionType.Farmer => BuildingType.Farm,
        ProfessionType.Miner => BuildingType.Mine,
        ProfessionType.Doctor => BuildingType.Clinic,
        ProfessionType.Craftsman => BuildingType.Furnace,
        ProfessionType.Student => BuildingType.School,
        ProfessionType.Teacher => BuildingType.School,
        ProfessionType.Guard => BuildingType.GuardPost,
        ProfessionType.Lumberjack => BuildingType.LumberCamp,
        _ => BuildingType.None
    };

    public static ProfessionType GetProfessionForWorkplace(BuildingType type) => type switch
    {
        BuildingType.Farm => ProfessionType.Farmer,
        BuildingType.Mine => ProfessionType.Miner,
        BuildingType.Clinic => ProfessionType.Doctor,
        BuildingType.Furnace => ProfessionType.Craftsman,
        BuildingType.GuardPost => ProfessionType.Guard,
        BuildingType.LumberCamp => ProfessionType.Lumberjack,
        _ => ProfessionType.None
    };

    public static bool NeedsQuarantineHousing(in ResidentData resident) =>
        resident.healthStatus == HealthStatus.ActiveInfected
        || resident.healthStatus == HealthStatus.Incubating;

    public static bool CanWork(in ResidentData resident)
    {
        if (!resident.isAlive)
            return false;

        if (resident.GetAgeGroup() != AgeGroupType.Adult)
            return false;

        if (resident.healthStatus == HealthStatus.ActiveInfected)
            return false;

        if (resident.professionType == ProfessionType.None
            || resident.professionType == ProfessionType.Student)
            return false;

        return GetPreferredWorkplace(resident.professionType) != BuildingType.None;
    }

    public static bool CanStudy(in ResidentData resident)
    {
        if (!resident.isAlive)
            return false;

        if (resident.GetAgeGroup() != AgeGroupType.Child)
            return false;

        if (resident.healthStatus == HealthStatus.ActiveInfected)
            return false;

        return resident.professionType == ProfessionType.Student;
    }

    public static bool NeedsHousing(in ResidentData resident) =>
        resident.isAlive && resident.assignedHouseID < 0;

    public static bool IsHousedInQuarantine(
        in ResidentData resident,
        BuildingData[] buildings,
        int buildingCount)
    {
        if (resident.assignedHouseID < 0 || buildings == null)
            return false;

        for (int i = 0; i < buildingCount; i++)
        {
            if (buildings[i].buildingID != (ushort)resident.assignedHouseID)
                continue;
            return buildings[i].buildingType == BuildingType.QuarantineWard;
        }

        return false;
    }

    public static bool NeedsWorkplace(in ResidentData resident) =>
        (CanWork(in resident) || CanStudy(in resident)) && resident.assignedWorkID < 0;

    public static bool IsBuildingAcceptingResidents(in BuildingData building) =>
        building.buildingState == BuildingState.Active
        && building.isOperational
        && building.isAllocated;

    public static bool HasHousingSlot(in BuildingData building) =>
        IsHousingBuilding(building.buildingType)
        && IsBuildingAcceptingResidents(in building)
        && building.currentOccupancy < building.maxOccupancy;

    public static bool HasWorkSlot(in BuildingData building) =>
        IsWorkplaceBuilding(building.buildingType)
        && IsBuildingAcceptingResidents(in building)
        && building.currentWorkers < building.maxWorkers;

    public static bool IsValidHousingForResident(in ResidentData resident, in BuildingData building)
    {
        if (!HasHousingSlot(in building))
            return false;

        bool needsQuarantine = NeedsQuarantineHousing(in resident);
        if (needsQuarantine)
            return building.buildingType == BuildingType.QuarantineWard
                || building.buildingType == BuildingType.House;

        return building.buildingType == BuildingType.House;
    }

    public static bool HasStudentSlot(in BuildingData building) =>
        building.buildingType == BuildingType.School
        && IsBuildingAcceptingResidents(in building)
        && building.currentOccupancy < building.maxOccupancy;

    public static bool IsValidWorkplaceForResident(in ResidentData resident, in BuildingData building)
    {
        BuildingType preferred = GetPreferredWorkplace(resident.professionType);
        if (preferred == BuildingType.None || building.buildingType != preferred)
            return false;

        if (CanStudy(in resident))
            return HasStudentSlot(in building);

        if (CanWork(in resident))
            return HasWorkSlot(in building);

        return false;
    }

    public static void OccupyWorkplaceSlot(ref BuildingData building, in ResidentData resident)
    {
        if (resident.professionType == ProfessionType.Student)
            building.currentOccupancy++;
        else
            building.currentWorkers++;
    }

    public static int HousingPriority(in ResidentData resident, in BuildingData building)
    {
        if (!IsValidHousingForResident(in resident, in building))
            return -1;

        int bonus = ResidentSocialRules.GetHousingPriorityBonus(in resident);

        if (NeedsQuarantineHousing(in resident) && building.buildingType == BuildingType.QuarantineWard)
            return 100;

        if (!NeedsQuarantineHousing(in resident) && building.buildingType == BuildingType.House)
            return 80 + bonus;

        return 40 + bonus;
    }

    public static void ClearHousingAssignment(ref ResidentData resident) =>
        resident.assignedHouseID = UnassignedId;

    public static void ClearWorkAssignment(ref ResidentData resident) =>
        resident.assignedWorkID = UnassignedId;

    public static void AssignHousing(ref ResidentData resident, ushort buildingId) =>
        resident.assignedHouseID = (short)buildingId;

    public static void AssignWorkplace(ref ResidentData resident, ushort buildingId) =>
        resident.assignedWorkID = (short)buildingId;
}
