using System;
using System.Collections.Generic;
using UnityEngine;

public class ResidentManager : MonoBehaviour
{
    public const int MAX_RESIDENTS = 10000;
    private const byte HungerMonthsBeforeDeathRisk = 2;
    private const byte ColdMonthsBeforeDeathRisk = 2;

    [Header("Dữ Liệu Mảng Tĩnh Lõi")]
    public ResidentData[] allResidents = new ResidentData[MAX_RESIDENTS];
    public int activeCount = 0;

    [Header("Visual Pool")]
    [SerializeField] private ResidentBase residentPrefab;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private BuildingManager buildingManager;
    [SerializeField] private GlobalSystemManager globalSystemManager;
    [SerializeField] private int maxVisualAgents = 200;
    [SerializeField] private int poolPreloadCount = 32;

    [Header("Đói / Chết đói")]
    [SerializeField] [Range(0f, 1f)] private float starveDeathChance = 0.1f;
    [SerializeField] private float hungerHappinessPenalty = 0.1f;
    [SerializeField] private float coldHappinessPenalty = 0.1f;

    [Header("Bệnh mẫu — RedFever")]
    [SerializeField] [Range(0f, 1f)] private float diseaseSpreadFactor = 0.15f;
    [SerializeField] [Range(0f, 1f)] private float naturalRecoveryChance = 0.08f;

    [Header("Grid → World")]
    [SerializeField] private Vector2 gridOrigin;
    [SerializeField] private float cellSize = 1f;

    private readonly List<ResidentBase> _activeVisuals = new List<ResidentBase>();
    private readonly bool[] _visualBoundFlags = new bool[MAX_RESIDENTS];

    public void Init()
    {
        ResolveDependencies();

        if (residentPrefab != null && poolPreloadCount > 0)
            SimplePool2.PoolPreLoad(residentPrefab.gameObject, poolPreloadCount, visualRoot);
    }

    private void OnEnable()
    {
        ResolveDependencies();
        SubscribeEvents();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    private void SubscribeEvents()
    {
        GameEvents.Unlisten(EventID.HungerResolved, OnHungerResolved);
        GameEvents.Unlisten(EventID.ColderResolved, OnColderResolved);
        GameEvents.Unlisten(EventID.DiseasePressure, OnDiseasePressure);
        GameEvents.Unlisten(EventID.YearChanged, OnYearChanged);
        GameEvents.Unlisten(EventID.MonthChanged, OnMonthChanged);

        GameEvents.Listen(EventID.HungerResolved, OnHungerResolved);
        GameEvents.Listen(EventID.ColderResolved, OnColderResolved);
        GameEvents.Listen(EventID.DiseasePressure, OnDiseasePressure);
        GameEvents.Listen(EventID.YearChanged, OnYearChanged);
        GameEvents.Listen(EventID.MonthChanged, OnMonthChanged);
    }

    private void UnsubscribeEvents()
    {
        GameEvents.Unlisten(EventID.HungerResolved, OnHungerResolved);
        GameEvents.Unlisten(EventID.ColderResolved, OnColderResolved);
        GameEvents.Unlisten(EventID.DiseasePressure, OnDiseasePressure);
        GameEvents.Unlisten(EventID.YearChanged, OnYearChanged);
        GameEvents.Unlisten(EventID.MonthChanged, OnMonthChanged);
    }

    private void OnDestroy()
    {
        ClearAllVisualAgents();
    }

    private void ResolveDependencies()
    {
        if (GamePlayController.Instance == null || GamePlayController.Instance.playerContain == null)
            return;

        PlayerContain contain = GamePlayController.Instance.playerContain;
        if (buildingManager == null)
            buildingManager = contain.buildingManager;
        if (globalSystemManager == null)
            globalSystemManager = contain.globalSystemManager;
    }

    public int AddResident(in ResidentData newResident)
    {
        if (activeCount >= MAX_RESIDENTS)
        {
            Debug.LogError("[ResidentManager] Đã đạt giới hạn dân số tối đa!");
            return -1;
        }

        int index = activeCount;
        allResidents[index] = newResident;
        allResidents[index].isAlive = true;
        allResidents[index].hungerMonths = 0;
        allResidents[index].coldMonths = 0;
        allResidents[index].diseaseType = DiseaseType.None;
        allResidents[index].healthStatus = HealthStatus.Healthy;
        allResidents[index].IncubationMonths = 0;
        allResidents[index].recoveryMonths = 0;
        allResidents[index].symptoms = SymptomFlags.None;
        ResidentSocialRules.EnsureFaction(ref allResidents[index]);
        if (allResidents[index].professionType == ProfessionType.None)
            allResidents[index].professionType = ResidentProfessionRules.GetInitialProfession(in allResidents[index]);
        activeCount++;
        return index;
    }

    public ref ResidentData GetResidentRef(int index)
    {
        if ((uint)index >= (uint)activeCount)
            throw new ArgumentOutOfRangeException(nameof(index));

        return ref allResidents[index];
    }

    private void OnYearChanged(object param)
    {
        ProcessYearlyAging();
    }

    private void ProcessYearlyAging()
    {
        int aliveBefore = 0;
        int deaths = 0;

        for (int i = 0; i < activeCount; i++)
        {
            ref ResidentData r = ref allResidents[i];
            if (!r.isAlive)
                continue;

            aliveBefore++;
            if (r.age < byte.MaxValue)
                r.age++;

            if (ResidentAgingRules.ShouldRetire(in r))
                ResidentAgingRules.Retire(ref r);

            float deathChance = ResidentAgingRules.GetOldAgeDeathChance(in r);
            if (deathChance <= 0f || UnityEngine.Random.value >= deathChance)
                continue;

            ResidentAgingRules.DieOfAge(ref r);
            deaths++;
        }

        if (globalSystemManager != null && aliveBefore > 0 && deaths > 0)
        {
            ref GlobalSystemData global = ref globalSystemManager.GetGlobalDataRef();
            float sampleDeathRate = (float)deaths / aliveBefore;
            int macroDeaths = Mathf.RoundToInt(global.totalPopulation * sampleDeathRate);
            global.totalPopulation = Mathf.Max(0, global.totalPopulation - macroDeaths);
        }

        RefreshMetricsCache();
        SyncAllVisualAgents();
    }

    private void OnHungerResolved(object param)
    {
        ProcessMonthlyHunger((float)param);
    }

    private void OnColderResolved(object param)
    {
        ProcessMonthlyColder((float)param);
    }

    private void OnDiseasePressure(object param)
    {
        ProcessMonthlyDisease((float)param);
    }

    private void OnMonthChanged(object param)
    {
        MonthChangedPayload payload = (MonthChangedPayload)param;
        ProcessMonthlyAssignment(payload.year, payload.month, payload.season);
    }

    public void ProcessMonthlyHunger(float hungryRate)
    {
        hungryRate = Mathf.Clamp01(hungryRate);

        int aliveBefore = 0;
        int deaths = 0;

        for (int i = 0; i < activeCount; i++)
        {
            ref ResidentData r = ref allResidents[i];
            if (!r.isAlive)
                continue;

            aliveBefore++;

            bool hungryThisMonth = hungryRate > 0f && UnityEngine.Random.value < hungryRate;
            if (hungryThisMonth)
            {
                if (r.hungerMonths < byte.MaxValue)
                    r.hungerMonths++;

                r.happiness = Mathf.Max(0f, r.happiness - hungerHappinessPenalty);
            }
            else
            {
                r.hungerMonths = 0;
            }

            if (r.hungerMonths >= HungerMonthsBeforeDeathRisk
                && UnityEngine.Random.value < starveDeathChance)
            {
                r.isAlive = false;
                r.hungerMonths = 0;
                deaths++;
            }
        }

        if (globalSystemManager != null && aliveBefore > 0 && deaths > 0)
        {
            ref GlobalSystemData global = ref globalSystemManager.GetGlobalDataRef();
            float sampleDeathRate = (float)deaths / aliveBefore;
            int macroDeaths = Mathf.RoundToInt(global.totalPopulation * sampleDeathRate);
            global.totalPopulation = Mathf.Max(0, global.totalPopulation - macroDeaths);
        }

        RefreshMetricsCache();
        SyncAllVisualAgents();
    }
    public void ProcessMonthlyColder(float coldRate)
    {
        coldRate = Mathf.Clamp01(coldRate);
        int aliveBefore = 0;
        int deaths = 0;
        for(int i = 0; i < activeCount; i++)
        {
            ref ResidentData r = ref allResidents[i];
            if(!r.isAlive)
                continue;
            aliveBefore++;
            bool coldThisMonth = coldRate > 0f && UnityEngine.Random.value < coldRate;
            if (coldThisMonth)
            {
                if(r.coldMonths < byte.MaxValue)
                    r.coldMonths++;
                r.happiness = Mathf.Max(0f, r.happiness - coldHappinessPenalty);
            }
            else r.coldMonths = 0;
            if(r.coldMonths >= ColdMonthsBeforeDeathRisk && UnityEngine.Random.value < starveDeathChance)
            {
                r.isAlive = false;
                r.coldMonths = 0;
                deaths++;
            }
        }
        if(globalSystemManager != null && aliveBefore > 0 && deaths > 0)
        {
            ref GlobalSystemData global = ref globalSystemManager.GetGlobalDataRef();
            float sampleDeathRate = (float)deaths / aliveBefore;
            int macroDeaths = Mathf.RoundToInt(global.totalPopulation * sampleDeathRate);
            global.totalPopulation = Mathf.Max(0, global.totalPopulation - macroDeaths);
        }
        RefreshMetricsCache();
        SyncAllVisualAgents();
    }

    public void ProcessMonthlyDisease(float diseasePressure)
    {
        ResidentDiseaseSystem.ProcessMonthly(
            allResidents,
            activeCount,
            diseasePressure,
            diseaseSpreadFactor,
            naturalRecoveryChance,
            globalSystemManager);

        RefreshMetricsCache();
        SyncAllVisualAgents();
    }

    public void ProcessMonthlyAssignment(int year, byte month, SeasonType season)
    {
        if (buildingManager == null)
            ResolveDependencies();

        if (buildingManager == null)
            return;

        ResidentProfessionSystem.ProcessMonthly(
            allResidents,
            activeCount,
            buildingManager.allBuildings,
            buildingManager.activeCount);

        ResidentAssignmentSystem.ProcessMonthly(
            allResidents,
            activeCount,
            buildingManager.allBuildings,
            buildingManager.activeCount);

        ResidentProfessionEffectSystem.ProcessMonthly(
            allResidents,
            activeCount,
            buildingManager.allBuildings,
            buildingManager.activeCount,
            globalSystemManager);

        ResidentSocialSystem.ProcessMonthly(
            allResidents,
            activeCount,
            globalSystemManager);

        ResidentReproductionSystem.ProcessMonthly(
            allResidents,
            ref activeCount,
            MAX_RESIDENTS,
            buildingManager.allBuildings,
            buildingManager.activeCount,
            season,
            globalSystemManager);

        RefreshMetricsCache();
        SyncAllVisualAgents();
        FillMissingVisualAgents();
    }

    public void RefreshMetricsCache()
    {
        if (globalSystemManager == null)
            return;

        int alive = 0;
        int infected = 0;
        float happinessSum = 0f;

        for (int i = 0; i < activeCount; i++)
        {
            ref ResidentData r = ref allResidents[i];
            if (!r.isAlive)
                continue;

            alive++;
            happinessSum += r.happiness;
            if (r.healthStatus == HealthStatus.ActiveInfected)
                infected++;
        }

        float avgHappiness = alive > 0 ? happinessSum / alive : 0f;

        ref GlobalSystemData global = ref globalSystemManager.GetGlobalDataRef();
        int pop = global.totalPopulation > 0 ? global.totalPopulation : alive;
        int scaledInfected = alive > 0
            ? Mathf.RoundToInt(pop * ((float)infected / alive))
            : 0;

        globalSystemManager.UpdateMetricsCache(pop, scaledInfected, avgHappiness);
    }

    public void RebindAllVisualAgents()
    {
        ClearAllVisualAgents();

        if (residentPrefab == null)
        {
            Debug.LogWarning("[ResidentManager] Chưa gán residentPrefab — bỏ qua visual rebind.");
            return;
        }

        ResolveDependencies();

        int spawned = 0;
        for (int i = 0; i < activeCount && spawned < maxVisualAgents; i++)
        {
            if (TrySpawnVisual(i, in allResidents[i]))
                spawned++;
        }

        Debug.Log($"[ResidentManager] Đã bind {spawned}/{activeCount} visual agent(s).");
    }

    public void SyncAllVisualAgents()
    {
        for (int i = _activeVisuals.Count - 1; i >= 0; i--)
        {
            ResidentBase view = _activeVisuals[i];
            if (view == null)
            {
                _activeVisuals.RemoveAt(i);
                continue;
            }

            if (!view.IsBound)
                continue;

            ref ResidentData data = ref GetResidentRef(view.DataIndex);
            if (!data.isAlive)
            {
                view.Unbind();
                SimplePool2.Despawn(view.gameObject);
                _activeVisuals.RemoveAt(i);
                continue;
            }

            view.SyncFromData(in data);
        }
    }

    private void FillMissingVisualAgents()
    {
        if (residentPrefab == null || _activeVisuals.Count >= maxVisualAgents)
            return;

        Array.Clear(_visualBoundFlags, 0, activeCount);
        for (int i = 0; i < _activeVisuals.Count; i++)
        {
            ResidentBase view = _activeVisuals[i];
            if (view == null || !view.IsBound)
                continue;

            if ((uint)view.DataIndex < (uint)activeCount)
                _visualBoundFlags[view.DataIndex] = true;
        }

        for (int i = 0; i < activeCount && _activeVisuals.Count < maxVisualAgents; i++)
        {
            if (_visualBoundFlags[i] || !allResidents[i].isAlive)
                continue;

            TrySpawnVisual(i, in allResidents[i]);
        }
    }

    private bool TrySpawnVisual(int index, in ResidentData resident)
    {
        if (residentPrefab == null || !resident.isAlive || _activeVisuals.Count >= maxVisualAgents)
            return false;

        Vector3 worldPosition = ResolveAgentPosition(in resident);
        ResidentBase view = SimplePool2.Spawn(residentPrefab, worldPosition, Quaternion.identity);

        if (visualRoot != null)
            view.transform.SetParent(visualRoot, true);

        view.Bind(index, in resident, worldPosition);
        _activeVisuals.Add(view);
        return true;
    }

    private void ClearAllVisualAgents()
    {
        for (int i = _activeVisuals.Count - 1; i >= 0; i--)
        {
            ResidentBase view = _activeVisuals[i];
            if (view == null)
                continue;

            view.Unbind();
            SimplePool2.Despawn(view.gameObject);
        }

        _activeVisuals.Clear();
    }

    private Vector3 ResolveAgentPosition(in ResidentData data)
    {
        if (buildingManager != null)
        {
            if (data.assignedWorkID >= 0)
            {
                int workIndex = buildingManager.FindBuildingIndexById((ushort)data.assignedWorkID);
                if (workIndex >= 0)
                {
                    ref BuildingData work = ref buildingManager.GetBuildingRef(workIndex);
                    return GridToWorld(work.coordX, work.coordY);
                }
            }

            if (data.assignedHouseID >= 0)
            {
                int houseIndex = buildingManager.FindBuildingIndexById((ushort)data.assignedHouseID);
                if (houseIndex >= 0)
                {
                    ref BuildingData house = ref buildingManager.GetBuildingRef(houseIndex);
                    return GridToWorld(house.coordX, house.coordY);
                }
            }
        }

        int fallbackX = data.residentID % 20;
        int fallbackY = (data.residentID / 20) % 20;
        return GridToWorld((ushort)fallbackX, (ushort)fallbackY);
    }

    private Vector3 GridToWorld(ushort coordX, ushort coordY)
    {
        return new Vector3(
            gridOrigin.x + coordX * cellSize,
            gridOrigin.y + coordY * cellSize,
            0f
        );
    }
}
