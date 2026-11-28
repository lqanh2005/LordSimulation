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

        if (buildings == null || buildingCount <= 0)
            return 0;

        int aliveBefore = 0;
        int parentCount = residentCount;
        int births = 0;
        for (int i = 0; i < parentCount; i++)
            if (residents[i].isAlive)
                aliveBefore++;

        // GDD v0.3 mục 5.4 + 9.6: mỗi nhà có >= 2 Adult và còn chỗ trống, mỗi tháng
        // có 5% × (Happiness trung bình của hộ / 50) khả năng sinh một Child.
        // Bằng 0 khi có người trong nhà đói hoặc nhà không được sưởi; Lều không sinh con.
        for (int h = 0; h < buildingCount && residentCount < maxResidents; h++)
        {
            ref BuildingData house = ref buildings[h];
            if (!ResidentReproductionRules.IsFamilyHouse(house.buildingType)
                || !ResidentAssignmentRules.HasHousingSlot(in house))
                continue;

            int motherIndex = -1, fatherIndex = -1, adults = 0, members = 0;
            float happinessSum = 0f;
            bool blocked = false;
            for (int i = 0; i < parentCount; i++)
            {
                ref ResidentData r = ref residents[i];
                if (!r.isAlive || r.assignedHouseID != (short)house.buildingID)
                    continue;
                members++;
                happinessSum += r.happiness;
                if (ResidentReproductionRules.BlocksBirth(in r))
                    blocked = true;
                if (!ResidentReproductionRules.CanReproduce(in r))
                    continue;
                adults++;
                if (r.gender == GenderType.Female && motherIndex < 0) motherIndex = i;
                else if (fatherIndex < 0) fatherIndex = i;
                else if (motherIndex < 0) motherIndex = i;
            }

            if (blocked || adults < ResidentReproductionRules.MinAdultsPerHouse || motherIndex < 0 || fatherIndex < 0)
                continue;

            float chance = ResidentReproductionRules.GetHouseBirthChance(happinessSum / members);
            if (chance <= 0f || Random.value >= chance)
                continue;

            int newId = NextResidentId(residents, residentCount);
            ResidentData child = ResidentReproductionRules.CreateChild(
                in residents[motherIndex], in residents[fatherIndex], newId);
            residents[residentCount] = child;
            residentCount++;
            births++;
            house.currentOccupancy++;
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
}
