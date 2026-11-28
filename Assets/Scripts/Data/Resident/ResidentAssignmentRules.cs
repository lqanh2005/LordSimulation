using UnityEngine;

public static class ResidentAssignmentRules
{
    public const short UnassignedId = -1;
    public const byte SchoolStartAge = 1;   // Tuổi nén GDD v0.3 (Child 0–3)

    public static bool IsHousingBuilding(BuildingType type) =>
        IsNormalHousing(type) || type == BuildingType.QuarantineWard;

    public static bool IsNormalHousing(BuildingType type) =>
        type == BuildingType.House || type == BuildingType.LargeHouse || type == BuildingType.Tent;

    public static bool IsWorkplaceBuilding(BuildingType type) => type switch
    {
        BuildingType.Farm => true,
        BuildingType.HerbFarm => true,
        BuildingType.Mine => true,
        BuildingType.CoalStore => true,
        BuildingType.Clinic => true,
        BuildingType.Furnace => true,
        BuildingType.Forge => true,
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

    /// <summary>Công trình phụ cho cùng nghề: nông dân làm vườn thuốc, thợ mỏ làm kho than.</summary>
    public static BuildingType GetSecondaryWorkplace(ProfessionType profession) => profession switch
    {
        ProfessionType.Farmer => BuildingType.HerbFarm,
        ProfessionType.Miner => BuildingType.CoalStore,
        ProfessionType.Craftsman => BuildingType.Forge,
        _ => BuildingType.None
    };

    public static bool IsWorkplaceFor(ProfessionType profession, BuildingType type)
    {
        if (type == BuildingType.None)
            return false;

        return type == GetPreferredWorkplace(profession) || type == GetSecondaryWorkplace(profession);
    }

    public static ProfessionType GetProfessionForWorkplace(BuildingType type) => type switch
    {
        BuildingType.Farm => ProfessionType.Farmer,
        BuildingType.HerbFarm => ProfessionType.Farmer,
        BuildingType.Mine => ProfessionType.Miner,
        BuildingType.CoalStore => ProfessionType.Miner,
        BuildingType.Clinic => ProfessionType.Doctor,
        BuildingType.Furnace => ProfessionType.Craftsman,
        BuildingType.Forge => ProfessionType.Craftsman,
        BuildingType.GuardPost => ProfessionType.Guard,
        BuildingType.LumberCamp => ProfessionType.Lumberjack,
        BuildingType.School => ProfessionType.Teacher,
        _ => ProfessionType.None
    };

    public static bool NeedsQuarantineHousing(in ResidentData resident) =>
        resident.quarantineMonths > 0   // Đang trong thời hạn cách ly (kể cả người khoẻ)
        || resident.healthStatus == HealthStatus.ActiveInfected
        || resident.healthStatus == HealthStatus.Incubating
        || resident.healthStatus == HealthStatus.Treated;

    /// <summary>
    /// Thành biết là phải cách ly (để XẾP VÀO Trạm): còn hạn cách ly hoặc đang phát bệnh rõ.
    /// Người đang ủ bệnh chưa lộ triệu chứng thì không ai biết -> không tự đưa vào Trạm (tránh lộ bệnh ẩn,
    /// tránh chiếm chỗ của người cách ly từ Cổng). Người đã ở Trạm thì vẫn ở lại theo NeedsQuarantineHousing.
    /// </summary>
    public static bool NeedsKnownQuarantine(in ResidentData resident) =>
        resident.quarantineMonths > 0
        || resident.healthStatus == HealthStatus.ActiveInfected;

    public static bool CanWork(in ResidentData resident)
    {
        if (!resident.isAlive)
            return false;

        if (resident.GetAgeGroup() != AgeGroupType.Adult)
            return false;

        // GDD 7.4: Infected không làm việc; Recovering (Treated) làm được với 50% hiệu suất
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

        if (resident.GetAgeGroup() != AgeGroupType.Child || resident.age < SchoolStartAge)
            return false;

        if (resident.healthStatus == HealthStatus.ActiveInfected
            || resident.healthStatus == HealthStatus.Treated)
            return false;

        return resident.professionType == ProfessionType.Student;
    }

    public static bool NeedsClinicCare(in ResidentData resident) =>
        resident.isAlive
        && resident.healthStatus == HealthStatus.ActiveInfected;   // Nằm Infirmary khi Infected (GDD 7.4)

    public static bool IsClinicPatient(in ResidentData resident) =>
        NeedsClinicCare(in resident);

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
        (CanWork(in resident) || CanStudy(in resident) || NeedsClinicCare(in resident))
        && resident.assignedWorkID < 0;

    public static bool IsBuildingAcceptingResidents(in BuildingData building) =>
        building.buildingState == BuildingState.Active
        && building.isOperational
        && building.isAllocated;

    public static bool HasHousingSlot(in BuildingData building) =>
        IsHousingBuilding(building.buildingType)
        && IsBuildingAcceptingResidents(in building)
        && building.currentOccupancy < EmergencyOrders.GetHousingCapacity(in building);   // Dồn người: nhà gỗ 150%

    public static bool HasWorkSlot(in BuildingData building) =>
        IsWorkplaceBuilding(building.buildingType)
        && IsBuildingAcceptingResidents(in building)
        && building.currentWorkers < building.maxWorkers;

    public static bool IsValidHousingForResident(in ResidentData resident, in BuildingData building)
    {
        if (!HasHousingSlot(in building))
            return false;

        return IsAllowedNewHousingType(in resident, building.buildingType);
    }

    /// <summary>Được Ở LẠI loại nhà này không: người còn mang bệnh / đang điều trị / còn hạn được ở lại Trạm.</summary>
    public static bool IsAllowedHousingType(in ResidentData resident, BuildingType type)
    {
        if (NeedsQuarantineHousing(in resident))
            return type == BuildingType.QuarantineWard || IsNormalHousing(type);

        return IsNormalHousing(type);
    }

    /// <summary>Được XẾP MỚI vào loại nhà này không: chỉ người thành biết cần cách ly mới được đưa vào Trạm.</summary>
    public static bool IsAllowedNewHousingType(in ResidentData resident, BuildingType type)
    {
        if (NeedsKnownQuarantine(in resident))
            return type == BuildingType.QuarantineWard || IsNormalHousing(type);

        return IsNormalHousing(type);
    }

    public static bool HasStudentSlot(in BuildingData building) =>
        building.buildingType == BuildingType.School
        && IsBuildingAcceptingResidents(in building)
        && building.currentOccupancy < building.maxOccupancy;

    public static int GetClinicPatientCapacity(in BuildingData building)
    {
        if (building.maxOccupancy > 0)
            return building.maxOccupancy;

        int cap = building.maxWorkers * 4;
        if (cap < 8)
            cap = 8;
        if (cap > byte.MaxValue)
            cap = byte.MaxValue;
        return cap;
    }

    public static bool HasPatientSlot(in BuildingData building) =>
        building.buildingType == BuildingType.Clinic
        && IsBuildingAcceptingResidents(in building)
        && building.currentOccupancy < GetClinicPatientCapacity(in building);

    public static bool IsAssignedToClinic(
        in ResidentData resident,
        BuildingData[] buildings,
        int buildingCount)
    {
        if (resident.assignedWorkID < 0 || buildings == null)
            return false;

        for (int i = 0; i < buildingCount; i++)
        {
            if (buildings[i].buildingID != (ushort)resident.assignedWorkID)
                continue;
            return buildings[i].buildingType == BuildingType.Clinic;
        }

        return false;
    }

    public static bool IsValidWorkplaceForResident(in ResidentData resident, in BuildingData building)
    {
        if (NeedsClinicCare(in resident))
            return HasPatientSlot(in building);

        if (!IsWorkplaceFor(resident.professionType, building.buildingType))
            return false;

        if (CanStudy(in resident))
            return HasStudentSlot(in building);

        if (CanWork(in resident))
            return HasWorkSlot(in building);

        return false;
    }

    public static void OccupyWorkplaceSlot(ref BuildingData building, in ResidentData resident)
    {
        if (IsClinicPatient(in resident) || resident.professionType == ProfessionType.Student)
            building.currentOccupancy++;
        else
            building.currentWorkers++;
    }

    public static int HousingPriority(in ResidentData resident, in BuildingData building)
    {
        if (!IsValidHousingForResident(in resident, in building))
            return -1;

        int bonus = ResidentSocialRules.GetHousingPriorityBonus(in resident);

        bool knownQuarantine = NeedsKnownQuarantine(in resident);
        if (knownQuarantine && building.buildingType == BuildingType.QuarantineWard)
            return 100;

        if (!knownQuarantine && building.buildingType == BuildingType.LargeHouse)
            return 85 + bonus;

        if (!knownQuarantine && building.buildingType == BuildingType.House)
            return 80 + bonus;

        if (!knownQuarantine && building.buildingType == BuildingType.Tent)
            return 60 + bonus;

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
