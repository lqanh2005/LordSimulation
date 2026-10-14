using UnityEngine;

public static class ResidentProductionSystem
{
    private const int ResourceSlots = 8;

    private static readonly float[] WorkerPowerByBuilding = new float[BuildingManager.MAX_BUILDINGS];
    private static readonly int[] BuildingIndexById = CreateIndexLookup();
    private static readonly float[] PendingByResource = new float[ResourceSlots];
    private static readonly float[] CarryByResource = new float[ResourceSlots];

    public static void ProcessMonthly(
        ResidentData[] residents,
        int residentCount,
        BuildingData[] buildings,
        int buildingCount,
        SeasonType season,
        GlobalSystemManager global,
        EdictManager edicts)
    {
        if (residents == null || buildings == null || global == null || buildingCount <= 0)
            return;

        buildingCount = Mathf.Min(buildingCount, BuildingManager.MAX_BUILDINGS);

        for (int i = 0; i < buildingCount; i++)
        {
            WorkerPowerByBuilding[i] = 0f;
            BuildingIndexById[buildings[i].buildingID] = i;
        }

        int aliveSample = 0;
        for (int i = 0; i < residentCount; i++)
        {
            ref ResidentData worker = ref residents[i];
            if (!worker.isAlive)
                continue;

            aliveSample++;
            if (!ResidentProductionRules.IsWorkingProducer(in worker))
                continue;

            int buildingIndex = BuildingIndexById[(ushort)worker.assignedWorkID];
            if (buildingIndex < 0 || buildingIndex >= buildingCount)
                continue;
            if (buildings[buildingIndex].buildingID != (ushort)worker.assignedWorkID)
                continue;

            WorkerPowerByBuilding[buildingIndex] += ResidentProductionRules.GetWorkerPower(in worker);
        }

        float sampleScale = GetSampleScale(global, aliveSample);

        for (int i = 0; i < ResourceSlots; i++)
            PendingByResource[i] = 0f;

        for (int i = 0; i < buildingCount; i++)
        {
            ref BuildingData building = ref buildings[i];
            BuildingIndexById[building.buildingID] = -1;

            float power = WorkerPowerByBuilding[i];
            if (power <= 0f
                || !ResidentProductionRules.IsProductionBuilding(building.buildingType)
                || !ResidentAssignmentRules.IsBuildingAcceptingResidents(in building))
            {
                if (ResidentProductionRules.IsProductionBuilding(building.buildingType))
                    building.outputStored = 0;
                continue;
            }

            if (!ResidentProductionRules.TryGetYield(
                    building.buildingType,
                    out ResourceType primary,
                    out float primaryPerWorker,
                    out ResourceType secondary,
                    out float secondaryPerWorker))
                continue;

            float multiplier = power
                * ResidentProductionRules.GetSeasonMultiplier(building.buildingType, season)
                * (edicts != null ? edicts.GetProductionMultiplier(building.buildingType) : 1f)
                * sampleScale;

            float primaryAmount = primaryPerWorker * multiplier;
            PendingByResource[(int)primary] += primaryAmount;
            if (secondary != ResourceType.None)
                PendingByResource[(int)secondary] += secondaryPerWorker * multiplier;

            building.outputStored = (ushort)Mathf.Min(ushort.MaxValue, Mathf.RoundToInt(primaryAmount));
        }

        for (int i = 0; i < ResourceSlots; i++)
        {
            float total = PendingByResource[i] + CarryByResource[i];
            int whole = Mathf.FloorToInt(total);
            CarryByResource[i] = total - whole;

            if (whole > 0)
                global.AddResource((ResourceType)i, whole);
        }
    }

    private static float GetSampleScale(GlobalSystemManager global, int aliveSample)
    {
        if (aliveSample <= 0)
            return 1f;

        int population = global.GetGlobalDataRef().totalPopulation;
        return population > aliveSample ? (float)population / aliveSample : 1f;
    }

    private static int[] CreateIndexLookup()
    {
        int[] lookup = new int[ushort.MaxValue + 1];
        for (int i = 0; i < lookup.Length; i++)
            lookup[i] = -1;
        return lookup;
    }
}
