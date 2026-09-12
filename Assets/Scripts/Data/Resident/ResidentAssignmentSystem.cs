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

            if (!ResidentAssignmentRules.CanWork(in r)
                && !ResidentAssignmentRules.CanStudy(in r)
                && r.assignedWorkID >= 0)
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

        if (!ResidentAssignmentRules.IsHousingBuilding(building.buildingType))
            return false;

        if (ResidentAssignmentRules.NeedsQuarantineHousing(in resident))
            return building.buildingType == BuildingType.QuarantineWard
                || building.buildingType == BuildingType.House;

        return building.buildingType == BuildingType.House;
    }

    private static bool IsStillValidWorkplace(in ResidentData resident, in BuildingData building)
    {
        if (!ResidentAssignmentRules.CanWork(in resident)
            && !ResidentAssignmentRules.CanStudy(in resident))
            return false;

        if (!ResidentAssignmentRules.IsBuildingAcceptingResidents(in building))
            return false;

        BuildingType preferred = ResidentAssignmentRules.GetPreferredWorkplace(resident.professionType);
        return preferred != BuildingType.None && building.buildingType == preferred;
    }

    private static void AssignMissingHousing(
        ResidentData[] residents,
        int residentCount,
        BuildingData[] buildings,
        int buildingCount)
    {
        for (int i = 0; i < residentCount; i++)
        {
            ref ResidentData r = ref residents[i];
            if (!ResidentAssignmentRules.NeedsHousing(in r))
                continue;

            int bestIndex = FindBestHousingIndex(in r, buildings, buildingCount);
            if (bestIndex < 0)
                continue;

            ResidentAssignmentRules.AssignHousing(ref r, buildings[bestIndex].buildingID);
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
