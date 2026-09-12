using UnityEngine;

public static class ResidentReproductionSystem
{
    public static int ProcessMonthly(
        ResidentData[] residents,
        ref int residentCount,
        int maxResidents,
        BuildingData[] buildings,
        int buildingCount,
        SeasonType season,
        GlobalSystemManager global)
    {
        if (residents == null || residentCount <= 0)
            return 0;

        int aliveBefore = 0;
        int parentCount = residentCount;
        int births = 0;

        for (int i = 0; i < parentCount; i++)
        {
            if (residents[i].isAlive)
                aliveBefore++;
        }

        for (int i = 0; i < parentCount && residentCount < maxResidents; i++)
        {
            ref ResidentData parent = ref residents[i];
            if (!ResidentReproductionRules.CanReproduce(in parent))
                continue;

            float chance = ResidentReproductionRules.GetBirthChance(in parent, season);
            if (chance <= 0f || Random.value >= chance)
                continue;

            int houseIndex = -1;
            if (buildings != null && parent.assignedHouseID >= 0)
                houseIndex = FindBuildingIndexById(buildings, buildingCount, (ushort)parent.assignedHouseID);

            if (houseIndex >= 0 && !ResidentAssignmentRules.HasHousingSlot(in buildings[houseIndex]))
                continue;

            int newId = NextResidentId(residents, residentCount);
            ResidentData child = ResidentReproductionRules.CreateChild(in parent, newId);
            if (houseIndex < 0)
                child.assignedHouseID = ResidentAssignmentRules.UnassignedId;

            residents[residentCount] = child;
            residentCount++;
            births++;

            if (houseIndex >= 0)
                buildings[houseIndex].currentOccupancy++;
        }

        if (global != null && aliveBefore > 0 && births > 0)
        {
            ref GlobalSystemData data = ref global.GetGlobalDataRef();
            float sampleBirthRate = (float)births / aliveBefore;
            int macroBirths = Mathf.RoundToInt(data.totalPopulation * sampleBirthRate);
            if (macroBirths < births)
                macroBirths = births;
            data.totalPopulation += macroBirths;
        }

        return births;
    }

    private static int NextResidentId(ResidentData[] residents, int residentCount)
    {
        int maxId = 0;
        for (int i = 0; i < residentCount; i++)
        {
            if (residents[i].residentID > maxId)
                maxId = residents[i].residentID;
        }

        return maxId + 1;
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
