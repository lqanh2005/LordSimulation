using UnityEngine;

public static class ResidentDiseaseSystem
{
    private const float MaxMonthlyInfectChance = 0.08f;
    private const float HungerDeathBonus = 0.02f;
    private const float ColdDeathBonus = 0.02f;
    private const float HouseContactChance = 0.22f;
    private const float WorkContactChance = 0.16f;
    private const float MaxContactChance = 0.45f;

    private static readonly DiseaseType[] ContactByBuildingId = new DiseaseType[ushort.MaxValue + 1];
    private static readonly bool[] ContactDirty = new bool[ushort.MaxValue + 1];
    private static readonly ushort[] DirtyBuildingIds = new ushort[BuildingManager.MAX_BUILDINGS];
    private static int dirtyCount;

    private static readonly float[] ClinicDoctorPower = new float[BuildingManager.MAX_BUILDINGS];
    private static readonly int[] ClinicPatientCount = new int[BuildingManager.MAX_BUILDINGS];

    public static int ProcessMonthly(
        ResidentData[] residents,
        int activeCount,
        BuildingData[] buildings,
        int buildingCount,
        float diseasePressure,
        float spreadFactor,
        float naturalRecoveryChance,
        GlobalSystemManager global)
    {
        diseasePressure = Mathf.Clamp01(diseasePressure);

        ClearContactMarks();

        int alive = 0;
        int carrierCount = 0;
        for (int i = 0; i < activeCount; i++)
        {
            ref ResidentData r = ref residents[i];
            if (!r.isAlive)
                continue;

            alive++;
            if (r.healthStatus != HealthStatus.ActiveInfected
                && r.healthStatus != HealthStatus.Incubating)
                continue;

            carrierCount++;
            MarkContactBuilding(r.assignedHouseID, r.diseaseType);
            MarkContactBuilding(r.assignedWorkID, r.diseaseType);
        }

        float infectedRate = alive > 0 ? (float)carrierCount / alive : 0f;
        float infectChance = Mathf.Min(
            MaxMonthlyInfectChance,
            diseasePressure * 0.5f + infectedRate * spreadFactor * 0.5f);

        CacheClinicCoverage(residents, activeCount, buildings, buildingCount);

        int deaths = 0;

        for (int i = 0; i < activeCount; i++)
        {
            ref ResidentData r = ref residents[i];
            if (!r.isAlive)
                continue;

            switch (r.healthStatus)
            {
                case HealthStatus.Healthy:
                    TryInfectHealthy(ref r, infectChance);
                    break;

                case HealthStatus.Incubating:
                    TickIncubation(ref r);
                    break;

                case HealthStatus.ActiveInfected:
                    if (TickActiveInfection(
                        ref r,
                        naturalRecoveryChance,
                        GetClinicCoverage(in r, buildings, buildingCount)))
                        deaths++;
                    break;

                case HealthStatus.Treated:
                    TickTreatment(ref r);
                    break;
            }
        }

        if (global != null && alive > 0 && deaths > 0)
        {
            ref GlobalSystemData globalData = ref global.GetGlobalDataRef();
            float sampleDeathRate = (float)deaths / alive;
            int macroDeaths = Mathf.RoundToInt(globalData.totalPopulation * sampleDeathRate);
            globalData.totalPopulation = Mathf.Max(0, globalData.totalPopulation - macroDeaths);
        }

        return deaths;
    }

    private static void TryInfectHealthy(ref ResidentData r, float cityChance)
    {
        float originMult = ResidentSocialRules.GetInfectionChanceMultiplier(r.originRegion);
        float chance = cityChance * originMult;
        DiseaseType contactType = DiseaseType.None;

        if (r.assignedHouseID >= 0)
        {
            DiseaseType houseType = ContactByBuildingId[(ushort)r.assignedHouseID];
            if (houseType != DiseaseType.None)
            {
                chance += HouseContactChance * originMult;
                contactType = houseType;
            }
        }

        if (r.assignedWorkID >= 0)
        {
            DiseaseType workType = ContactByBuildingId[(ushort)r.assignedWorkID];
            if (workType != DiseaseType.None)
            {
                chance += WorkContactChance * originMult;
                contactType = workType;
            }
        }

        if (chance > MaxContactChance)
            chance = MaxContactChance;

        if (chance <= 0f || Random.value >= chance)
            return;

        ResidentDiseaseRules.Infect(
            ref r,
            contactType != DiseaseType.None ? contactType : ResidentDiseaseRules.RollRandomDisease());
    }

    private static void TickIncubation(ref ResidentData r)
    {
        if (r.IncubationMonths < byte.MaxValue)
            r.IncubationMonths++;

        if (r.IncubationMonths >= ResidentDiseaseRules.GetIncubationLength(r.diseaseType))
            ResidentDiseaseRules.ActivateDisease(ref r);
    }

    private static bool TickActiveInfection(ref ResidentData r, float naturalRecoveryChance, float doctorCoverage)
    {
        DiseaseType type = r.diseaseType;
        r.happiness = Mathf.Max(0f, r.happiness - ResidentDiseaseRules.GetHappinessPenalty(type));

        float deathChance = ResidentProfessionEffectRules.ScaleDeathChance(
            ResidentDiseaseRules.GetDeathChance(type),
            doctorCoverage);
        if (r.hungerMonths > 0)
            deathChance += HungerDeathBonus;
        if (r.coldMonths > 0)
            deathChance += ColdDeathBonus;

        if (Random.value < deathChance)
        {
            r.isAlive = false;
            ResidentDiseaseRules.ClearDisease(ref r);
            return true;
        }

        if (Random.value < naturalRecoveryChance)
            ResidentDiseaseRules.BeginTreatment(ref r);

        return false;
    }

    private static void TickTreatment(ref ResidentData r)
    {
        if (r.recoveryMonths < byte.MaxValue)
            r.recoveryMonths++;

        if (r.recoveryMonths >= ResidentDiseaseRules.GetRecoveryLength(r.diseaseType))
            ResidentDiseaseRules.ClearDisease(ref r);
    }

    private static void MarkContactBuilding(short buildingId, DiseaseType type)
    {
        if (buildingId < 0 || type == DiseaseType.None)
            return;

        ushort id = (ushort)buildingId;
        if (ContactDirty[id])
            return;

        ContactDirty[id] = true;
        ContactByBuildingId[id] = type;
        if (dirtyCount < DirtyBuildingIds.Length)
            DirtyBuildingIds[dirtyCount++] = id;
    }

    private static void ClearContactMarks()
    {
        for (int i = 0; i < dirtyCount; i++)
        {
            ushort id = DirtyBuildingIds[i];
            ContactByBuildingId[id] = DiseaseType.None;
            ContactDirty[id] = false;
        }

        dirtyCount = 0;
    }

    private static void CacheClinicCoverage(
        ResidentData[] residents,
        int activeCount,
        BuildingData[] buildings,
        int buildingCount)
    {
        for (int i = 0; i < buildingCount; i++)
        {
            ClinicDoctorPower[i] = 0f;
            ClinicPatientCount[i] = 0;
        }

        if (buildings == null || buildingCount <= 0)
            return;

        for (int i = 0; i < activeCount; i++)
        {
            ref ResidentData r = ref residents[i];
            if (!r.isAlive || r.assignedWorkID < 0)
                continue;

            int index = FindBuildingIndexById(buildings, buildingCount, (ushort)r.assignedWorkID);
            if (index < 0 || buildings[index].buildingType != BuildingType.Clinic)
                continue;

            if (ResidentProfessionEffectRules.IsWorkingDoctor(in r))
                ClinicDoctorPower[index] += r.WorkMultiplier;

            if (r.healthStatus == HealthStatus.ActiveInfected)
                ClinicPatientCount[index]++;
        }
    }

    private static float GetClinicCoverage(
        in ResidentData resident,
        BuildingData[] buildings,
        int buildingCount)
    {
        if (buildings == null || resident.assignedWorkID < 0)
            return 0f;

        int index = FindBuildingIndexById(buildings, buildingCount, (ushort)resident.assignedWorkID);
        if (index < 0 || buildings[index].buildingType != BuildingType.Clinic)
            return 0f;

        return ResidentProfessionEffectRules.GetDoctorCoverage(
            ClinicDoctorPower[index],
            ClinicPatientCount[index]);
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
