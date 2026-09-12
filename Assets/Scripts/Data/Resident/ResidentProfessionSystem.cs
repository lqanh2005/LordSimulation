public static class ResidentProfessionSystem
{
    private static readonly ProfessionType[] LaborDemandOrder =
    {
        ProfessionType.Doctor,
        ProfessionType.Teacher,
        ProfessionType.Guard,
        ProfessionType.Farmer,
        ProfessionType.Miner,
        ProfessionType.Lumberjack,
        ProfessionType.Craftsman
    };

    public static int ProcessMonthly(
        ResidentData[] residents,
        int residentCount,
        BuildingData[] buildings,
        int buildingCount)
    {
        if (residents == null || buildings == null)
            return 0;

        int changed = 0;
        for (int i = 0; i < residentCount; i++)
        {
            ref ResidentData r = ref residents[i];
            if (ResidentProfessionRules.TryGraduateStudent(ref r))
                changed++;
            else if (ResidentProfessionRules.TryAssignStudent(ref r))
                changed++;
        }

        for (int p = 0; p < LaborDemandOrder.Length; p++)
        {
            ProfessionType profession = LaborDemandOrder[p];
            int openSlots = CountOpenLaborSlots(
                profession, residents, residentCount, buildings, buildingCount);
            if (openSlots <= 0)
                continue;

            while (openSlots > 0)
            {
                int bestIndex = FindBestCandidate(profession, residents, residentCount);
                if (bestIndex < 0)
                    break;

                if (!ResidentProfessionRules.TryAssignProfession(ref residents[bestIndex], profession))
                    break;

                openSlots--;
                changed++;
            }
        }

        return changed;
    }

    private static int CountOpenLaborSlots(
        ProfessionType profession,
        ResidentData[] residents,
        int residentCount,
        BuildingData[] buildings,
        int buildingCount)
    {
        int capacity = 0;
        for (int i = 0; i < buildingCount; i++)
            capacity += ResidentProfessionRules.GetLaborCapacity(in buildings[i], profession);

        int employed = 0;
        for (int i = 0; i < residentCount; i++)
        {
            if (ResidentProfessionRules.CountsTowardLabor(in residents[i], profession))
                employed++;
        }

        int open = capacity - employed;
        return open > 0 ? open : 0;
    }

    private static int FindBestCandidate(
        ProfessionType profession,
        ResidentData[] residents,
        int residentCount)
    {
        int bestIndex = -1;
        int bestAffinity = 0;

        for (int i = 0; i < residentCount; i++)
        {
            ref ResidentData r = ref residents[i];
            if (!ResidentProfessionRules.CanReceiveProfession(in r))
                continue;

            int affinity = ResidentSocialRules.GetProfessionAffinity(in r, profession);
            if (affinity > bestAffinity)
            {
                bestAffinity = affinity;
                bestIndex = i;
            }
        }

        return bestIndex;
    }
}
