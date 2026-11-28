using UnityEngine;

public static class ResidentAssignmentSystem
{
    public static void ProcessMonthly(
        ResidentData[] residents,
        int residentCount,
        BuildingData[] buildings,
        int buildingCount)
    {
        if (residents == null || buildings == null)
            return;

        ResetBuildingSlots(buildings, buildingCount);
        ValidateAndRecount(residents, residentCount, buildings, buildingCount);
        MoveSickToQuarantine(residents, residentCount, buildings, buildingCount);
        AssignMissingHousing(residents, residentCount, buildings, buildingCount);
        AssignMissingWorkplaces(residents, residentCount, buildings, buildingCount);
    }

    private static void ResetBuildingSlots(BuildingData[] buildings, int buildingCount)
    {
        for (int i = 0; i < buildingCount; i++)
        {
            buildings[i].currentOccupancy = 0;
            buildings[i].currentWorkers = 0;
        }
    }

    private static void ValidateAndRecount(
        ResidentData[] residents,
        int residentCount,
        BuildingData[] buildings,
        int buildingCount)
    {
        for (int i = 0; i < residentCount; i++)
        {
            ref ResidentData r = ref residents[i];
            if (!r.isAlive)
            {
                ResidentAssignmentRules.ClearHousingAssignment(ref r);
                ResidentAssignmentRules.ClearWorkAssignment(ref r);
                continue;
            }

            if (r.assignedWorkID >= 0
                && !ResidentAssignmentRules.NeedsClinicCare(in r)
                && (!ResidentAssignmentRules.CanWork(in r)
                    && !ResidentAssignmentRules.CanStudy(in r)
                    || ResidentAssignmentRules.IsHousedInQuarantine(in r, buildings, buildingCount)))
                ResidentAssignmentRules.ClearWorkAssignment(ref r);

            if (r.assignedHouseID >= 0)
            {
                int houseIndex = FindBuildingIndexById(buildings, buildingCount, (ushort)r.assignedHouseID);
                if (houseIndex < 0 || !IsStillValidHouse(in r, in buildings[houseIndex]))
                {
                    ResidentAssignmentRules.ClearHousingAssignment(ref r);
                }
                else
                {
                    buildings[houseIndex].currentOccupancy++;
                }
            }

            if (r.assignedWorkID >= 0)
            {
                int workIndex = FindBuildingIndexById(buildings, buildingCount, (ushort)r.assignedWorkID);
                if (workIndex < 0 || !IsStillValidWorkplace(in r, in buildings[workIndex]))
                {
                    ResidentAssignmentRules.ClearWorkAssignment(ref r);
                }
                else
                {
                    ResidentAssignmentRules.OccupyWorkplaceSlot(ref buildings[workIndex], in r);
                }
            }
        }
    }

    private static bool IsStillValidHouse(in ResidentData resident, in BuildingData building)
    {
        if (!ResidentAssignmentRules.IsBuildingAcceptingResidents(in building))
            return false;

        return ResidentAssignmentRules.IsAllowedHousingType(in resident, building.buildingType);
    }

    private static bool IsStillValidWorkplace(in ResidentData resident, in BuildingData building)
    {
        if (!ResidentAssignmentRules.IsBuildingAcceptingResidents(in building))
            return false;

        if (ResidentAssignmentRules.NeedsClinicCare(in resident))
            return building.buildingType == BuildingType.Clinic;

        if (!ResidentAssignmentRules.CanWork(in resident)
            && !ResidentAssignmentRules.CanStudy(in resident))
            return false;

        return ResidentAssignmentRules.IsWorkplaceFor(resident.professionType, building.buildingType);
    }

    /// <summary>
    /// Người đang ở nhà thường mà phát bệnh rõ -> chuyển sang Trạm cách ly còn chỗ (không để lây cho cả nhà).
    /// Hết bệnh thì IsStillValidHouse tự trả họ về nhà thường. Hết chỗ trong Trạm thì ở lại nhà.
    /// </summary>
    private static void MoveSickToQuarantine(
        ResidentData[] residents,
        int residentCount,
        BuildingData[] buildings,
        int buildingCount)
    {
        int wardIndex = FindFreeWardIndex(buildings, buildingCount, 0);
        if (wardIndex < 0)
            return;

        for (int i = 0; i < residentCount; i++)
        {
            ref ResidentData r = ref residents[i];
            if (!r.isAlive || r.assignedHouseID < 0 || !ResidentAssignmentRules.NeedsKnownQuarantine(in r))
                continue;

            int houseIndex = FindBuildingIndexById(buildings, buildingCount, (ushort)r.assignedHouseID);
            if (houseIndex < 0 || buildings[houseIndex].buildingType == BuildingType.QuarantineWard)
                continue;

            if (!ResidentAssignmentRules.HasHousingSlot(in buildings[wardIndex]))
            {
                wardIndex = FindFreeWardIndex(buildings, buildingCount, wardIndex + 1);
                if (wardIndex < 0)
                    return;
            }

            if (buildings[houseIndex].currentOccupancy > 0)
                buildings[houseIndex].currentOccupancy--;
            ResidentAssignmentRules.AssignHousing(ref r, buildings[wardIndex].buildingID);
            buildings[wardIndex].currentOccupancy++;
        }
    }

    private static int FindFreeWardIndex(BuildingData[] buildings, int buildingCount, int start)
    {
        for (int i = start; i < buildingCount; i++)
        {
            if (buildings[i].buildingType == BuildingType.QuarantineWard
                && ResidentAssignmentRules.HasHousingSlot(in buildings[i]))
                return i;
        }

        return -1;
    }

    private static void AssignMissingHousing(
        ResidentData[] residents,
        int residentCount,
        BuildingData[] buildings,
        int buildingCount)
    {
        FamilyHandled.Clear();
        for (int i = 0; i < residentCount; i++)
        {
            ref ResidentData r = ref residents[i];
            if (!ResidentAssignmentRules.NeedsHousing(in r))
                continue;

            // GDD v0.3 mục 4.3: gia đình ở chung một nhà; không nhà nào đủ chỗ cho cả nhà -> cả nhà Homeless
            if (r.familyID != 0 && !ResidentAssignmentRules.NeedsQuarantineHousing(in r))
            {
                if (FamilyHandled.Add(r.familyID))
                    AssignFamilyHousing(r.familyID, residents, residentCount, buildings, buildingCount);
                continue;
            }

            int bestIndex = FindBestHousingIndex(in r, buildings, buildingCount);
            if (bestIndex < 0)
                continue;

            ResidentAssignmentRules.AssignHousing(ref r, buildings[bestIndex].buildingID);
            buildings[bestIndex].currentOccupancy++;
        }
    }

    private static readonly System.Collections.Generic.HashSet<int> FamilyHandled = new System.Collections.Generic.HashSet<int>();
    private static readonly System.Collections.Generic.List<int> FamilyMembers = new System.Collections.Generic.List<int>(8);

    private static void AssignFamilyHousing(
        int familyId,
        ResidentData[] residents,
        int residentCount,
        BuildingData[] buildings,
        int buildingCount)
    {
        FamilyMembers.Clear();
        short homeId = ResidentAssignmentRules.UnassignedId;
        for (int i = 0; i < residentCount; i++)
        {
            ref ResidentData m = ref residents[i];
            if (!m.isAlive || m.familyID != familyId || ResidentAssignmentRules.NeedsQuarantineHousing(in m))
                continue;
            if (m.assignedHouseID < 0)
                FamilyMembers.Add(i);
            else if (homeId < 0)
                homeId = m.assignedHouseID;   // Người nhà đã có chỗ (vd. vừa ra khỏi trạm cách ly): ưu tiên về ở cùng
        }
        if (FamilyMembers.Count == 0)
            return;

        int need = FamilyMembers.Count;
        int bestIndex = -1;
        int bestPriority = -1;
        for (int b = 0; b < buildingCount; b++)
        {
            ref BuildingData house = ref buildings[b];
            if (house.buildingType == BuildingType.QuarantineWard)
                continue;
            int priority = ResidentAssignmentRules.HousingPriority(in residents[FamilyMembers[0]], in house);
            if (priority < 0 || EmergencyOrders.GetHousingCapacity(in house) - house.currentOccupancy < need)
                continue;
            if (homeId >= 0 && house.buildingID == (ushort)homeId)
                priority += 1000;
            if (priority > bestPriority)
            {
                bestPriority = priority;
                bestIndex = b;
            }
        }

        if (bestIndex < 0)
            return;

        foreach (int i in FamilyMembers)
        {
            ResidentAssignmentRules.AssignHousing(ref residents[i], buildings[bestIndex].buildingID);
            buildings[bestIndex].currentOccupancy++;
        }
    }

    private static void AssignMissingWorkplaces(
        ResidentData[] residents,
        int residentCount,
        BuildingData[] buildings,
        int buildingCount)
    {
        for (int i = 0; i < residentCount; i++)
        {
            ref ResidentData r = ref residents[i];
            if (ResidentAssignmentRules.IsHousedInQuarantine(in r, buildings, buildingCount)
                && !ResidentAssignmentRules.NeedsClinicCare(in r))
                continue;
            if (!ResidentAssignmentRules.NeedsWorkplace(in r))
                continue;

            int workIndex = FindOpenWorkplaceIndex(in r, buildings, buildingCount);
            if (workIndex < 0)
                continue;

            ResidentAssignmentRules.AssignWorkplace(ref r, buildings[workIndex].buildingID);
            ResidentAssignmentRules.OccupyWorkplaceSlot(ref buildings[workIndex], in r);
        }
    }

    private static int FindBestHousingIndex(
        in ResidentData resident,
        BuildingData[] buildings,
        int buildingCount)
    {
        int bestIndex = -1;
        int bestPriority = -1;

        for (int i = 0; i < buildingCount; i++)
        {
            int priority = ResidentAssignmentRules.HousingPriority(in resident, in buildings[i]);
            if (priority > bestPriority)
            {
                bestPriority = priority;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private static int FindOpenWorkplaceIndex(
        in ResidentData resident,
        BuildingData[] buildings,
        int buildingCount)
    {
        for (int i = 0; i < buildingCount; i++)
        {
            if (ResidentAssignmentRules.IsValidWorkplaceForResident(in resident, in buildings[i]))
                return i;
        }

        return -1;
    }

    private static int FindBuildingIndexById(
        BuildingData[] buildings,
        int buildingCount,
        ushort buildingId)
    {
        for (int i = 0; i < buildingCount; i++)
        {
            if (buildings[i].buildingID == buildingId)
                return i;
        }

        return -1;
    }
}
