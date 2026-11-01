public static class ResidentProfessionSystem
{
    private const int NewcomerPriority = 100;

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

    private static readonly int[] OpenSlotsByProfession = new int[(int)ProfessionType.Guard + 1];
    private static readonly int[] SurplusByProfession = new int[(int)ProfessionType.Guard + 1];

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

        for (int i = 0; i < OpenSlotsByProfession.Length; i++)
        {
            OpenSlotsByProfession[i] = 0;
            SurplusByProfession[i] = 0;
        }

        for (int p = 0; p < LaborDemandOrder.Length; p++)
        {
            ProfessionType profession = LaborDemandOrder[p];
            CountLaborSlots(
                profession, residents, residentCount, buildings, buildingCount,
                out OpenSlotsByProfession[(int)profession],
                out SurplusByProfession[(int)profession]);
        }

        for (int p = 0; p < LaborDemandOrder.Length; p++)
        {
            ProfessionType profession = LaborDemandOrder[p];

            while (OpenSlotsByProfession[(int)profession] > 0)
            {
                int bestIndex = FindBestCandidate(profession, residents, residentCount);
                if (bestIndex < 0)
                    break;

                ref ResidentData candidate = ref residents[bestIndex];
                ProfessionType previous = candidate.professionType;
                bool assigned = previous == ProfessionType.None
                    ? ResidentProfessionRules.TryAssignProfession(ref candidate, profession)
                    : ResidentProfessionRules.TryReassignIdleWorker(ref candidate, profession);
                if (!assigned)
                    break;

                OpenSlotsByProfession[(int)profession]--;
                if (previous != ProfessionType.None)
                {
                    OpenSlotsByProfession[(int)previous]++;
                    SurplusByProfession[(int)previous]--;
                }
                changed++;
            }
        }

        return changed;
    }

    private static void CountLaborSlots(
        ProfessionType profession,
        ResidentData[] residents,
        int residentCount,
        BuildingData[] buildings,
        int buildingCount,
        out int openSlots,
        out int surplus)
    {
        int capacity = 0;
        int reserved = 0;
        for (int i = 0; i < buildingCount; i++)
        {
            capacity += ResidentProfessionRules.GetLaborCapacity(in buildings[i], profession);
            reserved += ResidentProfessionRules.GetReservedLaborCapacity(in buildings[i], profession);
        }

        int employed = 0;
        for (int i = 0; i < residentCount; i++)
        {
            if (ResidentProfessionRules.CountsTowardLabor(in residents[i], profession))
                employed++;
        }

        openSlots = capacity - employed;
        surplus = employed - reserved;
    }

    private static int FindBestCandidate(
        ProfessionType profession,
        ResidentData[] residents,
        int residentCount)
    {
        int bestIndex = -1;
        int bestScore = 0;

        for (int i = 0; i < residentCount; i++)
        {
            ref ResidentData r = ref residents[i];
            int score;

            if (ResidentProfessionRules.CanReceiveProfession(in r))
            {
                int affinity = ResidentSocialRules.GetProfessionAffinity(in r, profession);
                if (affinity <= 0)
                    continue;
                score = NewcomerPriority + affinity;
            }
            else if (ResidentProfessionRules.IsIdleWorker(in r)
                && r.professionType != profession
                && SurplusByProfession[(int)r.professionType] > 0)
            {
                score = ResidentSocialRules.GetProfessionAffinity(in r, profession);
            }
            else
            {
                continue;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        return bestIndex;
    }
}
