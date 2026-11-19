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
    [SerializeField] private EdictManager edictManager;
    [SerializeField] private int maxVisualAgents = 200;
    [SerializeField] private int poolPreloadCount = 32;
    [Tooltip("Tốc độ đi bộ (đơn vị world/giây — 1 tile rộng ~1 đơn vị)")]
    [SerializeField] private float commuteSpeed = 0.8f;

    [Header("Tìm đường A*")]
    [Tooltip("Số lượt A* tối đa mỗi frame — tránh giật khi cả làng đổi ca cùng lúc")]
    [SerializeField] private int maxPathRequestsPerFrame = 8;

    [Header("Dân khởi đầu (New Game)")]
    [SerializeField] private int startingResidentCount = 5;
    [Tooltip("Tuổi dân khởi đầu — nằm trong nhóm Adult (18-64) để dùng sprite Young")]
    [SerializeField] private Vector2Int startingAgeRange = new Vector2Int(18, 30);

    [Header("Đói / Chết đói")]
    [SerializeField] [Range(0f, 1f)] private float starveDeathChance = 0.1f;
    [Header("Lạnh / Chết rét")]
    [SerializeField] [Range(0f, 1f)] private float freezeDeathChance = 0.1f;
    [SerializeField] private float hungerHappinessPenalty = 0.1f;
    [SerializeField] private float coldHappinessPenalty = 0.1f;

    [Header("Bệnh mẫu — RedFever")]
    [SerializeField] [Range(0f, 1f)] private float diseaseSpreadFactor = 0.15f;
    [SerializeField] [Range(0f, 1f)] private float naturalRecoveryChance = 0.08f;

    [Header("Grid → World")]
    [SerializeField] private Grid isometricGrid;
    [Tooltip("Dùng khi không có Unity Grid (GamePlayScene dùng GridController)")]
    [SerializeField] private GridController gridController;
    [SerializeField] private Vector2 gridOrigin;
    [SerializeField] private float cellSize = 1f;

    private readonly List<ResidentBase> _activeVisuals = new List<ResidentBase>();
    private readonly bool[] _visualBoundFlags = new bool[MAX_RESIDENTS];

    private GridPathfinder _pathfinder;
    private readonly List<Vector2Int> _cellPathBuffer = new List<Vector2Int>(64);
    private readonly List<Vector3> _worldPathBuffer = new List<Vector3>(64);
    private int _pathRoundRobin;

    private struct CommuteGoal
    {
        public Vector3 world;
        public Vector2Int cell;
        public ushort buildingId;
        public bool indoors;   // Tới nơi thì vào trong nhà (ẩn hình) — giờ ngủ
    }

    // ===== Lịch sinh hoạt theo vòng ngày–đêm (DayCycle) =====
    [Header("Lịch sinh hoạt (1 tháng = 1 vòng ngày–đêm)")]
    [Tooltip("Tỉ lệ lính gác / thầy thuốc làm ca đêm (1/N người)")]
    [SerializeField, Min(2)] private int nightShiftOneIn = 5;
    [Tooltip("Tỉ lệ người không đi làm ra quảng trường (Tòa thị chính) ban ngày (N/3)")]
    [SerializeField, Range(0, 3)] private int outingThirds = 2;

    private ushort _plazaBuildingId;
    private Vector3 _plazaWorld;
    private Vector2Int _plazaCell;
    private readonly List<CommuteGoal> _campfires = new List<CommuteGoal>(8);
    private float _goalHour;
    private bool _reassignPending;

    // ===== Debug: ép cư dân đi tới ô chỉ định (key = residentID, bền qua compact) =====
    private readonly Dictionary<int, CommuteGoal> _debugGoals = new Dictionary<int, CommuteGoal>();

    public float CommuteSpeed
    {
        get => commuteSpeed;
        set => commuteSpeed = Mathf.Max(0f, value);
    }

    public IReadOnlyList<ResidentBase> ActiveVisuals => _activeVisuals;
    public GridController GridController => gridController;
    public int DebugOverrideCount => _debugGoals.Count;

    /// <summary>Ép cư dân của <paramref name="view"/> đi tới ô <paramref name="cell"/> (bỏ qua lịch đi làm/về nhà).</summary>
    public bool DebugMoveTo(ResidentBase view, Vector2Int cell)
    {
        if (view == null || !view.IsBound || (uint)view.DataIndex >= (uint)activeCount || gridController == null)
            return false;
        if (!gridController.IsInside(cell.x, cell.y))
            return false;

        ref ResidentData data = ref GetResidentRef(view.DataIndex);
        Vector3 world = gridController.GridToWorld(cell.x, cell.y);
        world.z = 0f;
        _debugGoals[data.residentID] = new CommuteGoal
        {
            world = world,
            cell = cell,
            buildingId = gridController.GetOccupantId(cell.x, cell.y)
        };
        return true;
    }

    public void ClearDebugMoves()
    {
        _debugGoals.Clear();
    }

    public int GetResidentId(ResidentBase view)
    {
        if (view == null || !view.IsBound || (uint)view.DataIndex >= (uint)activeCount)
            return -1;
        return GetResidentRef(view.DataIndex).residentID;
    }

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
        GameEvents.Unlisten(EventID.BuildingDemolished, OnBuildingDemolished);
        GameEvents.Unlisten(EventID.BuildingCompleted, OnBuildingCompleted);

        GameEvents.Listen(EventID.HungerResolved, OnHungerResolved);
        GameEvents.Listen(EventID.ColderResolved, OnColderResolved);
        GameEvents.Listen(EventID.DiseasePressure, OnDiseasePressure);
        GameEvents.Listen(EventID.YearChanged, OnYearChanged);
        GameEvents.Listen(EventID.MonthChanged, OnMonthChanged);
        GameEvents.Listen(EventID.BuildingDemolished, OnBuildingDemolished);
        GameEvents.Listen(EventID.BuildingCompleted, OnBuildingCompleted);
    }

    private void UnsubscribeEvents()
    {
        GameEvents.Unlisten(EventID.HungerResolved, OnHungerResolved);
        GameEvents.Unlisten(EventID.ColderResolved, OnColderResolved);
        GameEvents.Unlisten(EventID.DiseasePressure, OnDiseasePressure);
        GameEvents.Unlisten(EventID.YearChanged, OnYearChanged);
        GameEvents.Unlisten(EventID.MonthChanged, OnMonthChanged);
        GameEvents.Unlisten(EventID.BuildingDemolished, OnBuildingDemolished);
        GameEvents.Unlisten(EventID.BuildingCompleted, OnBuildingCompleted);
    }

    // Công trình xây xong giữa tháng (Lều, Đường… xây tức thì) -> dân dọn vào / đi làm ngay
    private void OnBuildingCompleted(object param)
    {
        RequestReassign();
    }

    /// <summary>
    /// Xếp lại nghề / nhà / chỗ làm ngay (gom về LateUpdate, chạy 1 lần dù gọi nhiều lần trong frame).
    /// Gọi khi có người mới vào thành, công trình xây xong, bật/tắt công trình… để không ai phải đứng chờ tới tháng sau.
    /// </summary>
    public void RequestReassign()
    {
        _reassignPending = true;
    }

    private void LateUpdate()
    {
        if (!_reassignPending)
            return;
        _reassignPending = false;

        if (buildingManager == null)
            ResolveDependencies();
        if (buildingManager == null)
            return;

        ResidentProfessionSystem.ProcessMonthly(
            allResidents, activeCount, buildingManager.allBuildings, buildingManager.activeCount);
        ResidentAssignmentSystem.ProcessMonthly(
            allResidents, activeCount, buildingManager.allBuildings, buildingManager.activeCount);
        SyncAllVisualAgents();
        FillMissingVisualAgents();
    }

    // Công trình bị phá → cư dân mất nhà / chỗ làm ngay, rồi xếp lại chỗ mới luôn (không đợi sang tháng)
    private void OnBuildingDemolished(object param)
    {
        if (!(param is BuildingDemolishedPayload payload))
            return;

        int affected = 0;
        for (int i = 0; i < activeCount; i++)
        {
            ref ResidentData r = ref allResidents[i];
            if (r.assignedHouseID == payload.buildingId)
            {
                ResidentAssignmentRules.ClearHousingAssignment(ref r);
                affected++;
            }
            if (r.assignedWorkID == payload.buildingId)
            {
                ResidentAssignmentRules.ClearWorkAssignment(ref r);
                affected++;
            }
        }

        ResolveDependencies();
        if (affected > 0 && buildingManager != null)
        {
            ResidentAssignmentSystem.ProcessMonthly(
                allResidents,
                activeCount,
                buildingManager.allBuildings,
                buildingManager.activeCount);
        }
    }

    private void OnDestroy()
    {
        ClearAllVisualAgents();
    }

    private void ResolveDependencies()
    {
        if (isometricGrid == null && gridController == null)
            gridController = FindFirstObjectByType<GridController>();

        if (GamePlayController.Instance == null || GamePlayController.Instance.playerContain == null)
            return;

        PlayerContain contain = GamePlayController.Instance.playerContain;
        if (buildingManager == null)
            buildingManager = contain.buildingManager;
        if (globalSystemManager == null)
            globalSystemManager = contain.globalSystemManager;
        if (edictManager == null)
            edictManager = contain.edictManager;
    }

    private void EnsurePathfinder()
    {
        if (gridController == null && isometricGrid == null)
            gridController = FindFirstObjectByType<GridController>();

        // A* chỉ chạy trên GridController; Unity Grid cũ vẫn đi đường thẳng
        if (_pathfinder == null && gridController != null && isometricGrid == null)
            _pathfinder = new GridPathfinder(gridController);
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
        if (allResidents[index].bodyTemperature <= 0f)
            allResidents[index].bodyTemperature = ResidentDiseaseRules.NormalBodyTemperature;
        ResidentSocialRules.EnsureFaction(ref allResidents[index]);
        if (allResidents[index].professionType == ProfessionType.None)
            allResidents[index].professionType = ResidentProfessionRules.GetInitialProfession(in allResidents[index]);
        activeCount++;
        TrySpawnVisual(index, in allResidents[index]);
        return index;
    }

    public void ConfigureVisualWorld(
        Grid grid,
        Transform root,
        ResidentBase prefab,
        BuildingManager buildings,
        GlobalSystemManager global,
        float commute = -1f)
    {
        isometricGrid = grid;
        visualRoot = root;
        residentPrefab = prefab;
        buildingManager = buildings;
        globalSystemManager = global;
        if (commute > 0f)
            commuteSpeed = commute;
    }

    public Vector3 GetBuildingWorldPosition(in BuildingData building)
    {
        return CellToWorld(building.coordX, building.coordY, BuildingRules.GetFootprint(in building));
    }

    public Vector3 CellToWorld(int cellX, int cellY, int size = 1)
    {
        size = Mathf.Max(1, size);
        return CellToWorld(cellX, cellY, new Vector2Int(size, size));
    }

    public Vector3 CellToWorld(int cellX, int cellY, Vector2Int size)
    {
        size = Vector2Int.Max(Vector2Int.one, size);
        if (isometricGrid != null)
        {
            Vector3Int origin = new Vector3Int(cellX, cellY, 0);
            Vector3Int top = origin + new Vector3Int(size.x - 1, size.y - 1, 0);
            Vector3 world = (isometricGrid.GetCellCenterWorld(origin) + isometricGrid.GetCellCenterWorld(top)) * 0.5f;
            world.z = 0f;
            return world;
        }

        if (gridController != null)
        {
            Vector3 world = gridController.GetFootprintCenterWorld(new Vector2Int(cellX, cellY), size);
            world.z = 0f;
            return world;
        }

        return new Vector3(
            gridOrigin.x + cellX * cellSize,
            gridOrigin.y + cellY * cellSize,
            0f
        );
    }

    // Gọi một lần khi bắt đầu New Game (không gọi khi load save)
    public void SpawnStartingResidents()
    {
        if (startingResidentCount <= 0)
            return;

        int nextId = PeekMaxResidentId() + 1;
        int minAge = Mathf.Max(18, startingAgeRange.x);
        int maxAge = Mathf.Min(64, Mathf.Max(minAge, startingAgeRange.y));
        int added = 0;

        for (int i = 0; i < startingResidentCount; i++)
        {
            ResidentData resident = ImmigrationRules.CreateApplicant(nextId++);
            resident.age = (byte)UnityEngine.Random.Range(minAge, maxAge + 1);
            // Dân khởi đầu luôn khỏe mạnh — không mang bệnh tiềm ẩn như dân nhập cư
            resident.healthStatus = HealthStatus.Healthy;
            resident.diseaseType = DiseaseType.None;
            resident.IncubationMonths = 0;
            resident.recoveryMonths = 0;
            resident.symptoms = SymptomFlags.None;
            resident.bodyTemperature = ResidentDiseaseRules.NormalBodyTemperature;
            resident.professionType = ResidentProfessionRules.GetInitialProfession(in resident);

            if (AddResident(in resident) >= 0)
                added++;
        }

        if (globalSystemManager == null)
            ResolveDependencies();
        if (globalSystemManager != null)
            globalSystemManager.GetGlobalDataRef().totalPopulation += added;

        NotifyPopulationChanged();
    }

    public int PeekMaxResidentId()
    {
        int maxId = 0;
        for (int i = 0; i < activeCount; i++)
        {
            if (allResidents[i].residentID > maxId)
                maxId = allResidents[i].residentID;
        }

        return maxId;
    }

    public void NotifyPopulationChanged()
    {
        RefreshVisualPopulation();
        RequestReassign();   // Người mới vào thành có nhà / việc ngay, không đợi sang tháng
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

        ApplyMacroDeaths(deaths, aliveBefore, DeathCause.OldAge);

        RefreshVisualPopulation();
    }

    /// <summary>Quy số chết trong mẫu ra dân số thật, trừ vào tổng và báo cho UI.</summary>
    private void ApplyMacroDeaths(int deaths, int aliveBefore, DeathCause cause)
    {
        if (deaths <= 0 || aliveBefore <= 0)
            return;

        int macroDeaths = deaths;
        if (globalSystemManager != null)
        {
            ref GlobalSystemData global = ref globalSystemManager.GetGlobalDataRef();
            float sampleDeathRate = (float)deaths / aliveBefore;
            macroDeaths = Mathf.Min(global.totalPopulation, Mathf.RoundToInt(global.totalPopulation * sampleDeathRate));
            global.totalPopulation -= macroDeaths;
        }

        if (macroDeaths > 0)
            GameEvents.Post(EventID.ResidentsDied, new ResidentsDiedPayload { cause = cause, count = macroDeaths });
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
        if (!payload.isSimulationTick)
            return;

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

        ApplyMacroDeaths(deaths, aliveBefore, DeathCause.Starvation);

        RefreshVisualPopulation();
    }
    public void ProcessMonthlyColder(float coldRate)
    {
        coldRate = Mathf.Clamp01(coldRate);
        // GDD: vô gia cư không được sưởi -> mùa đông luôn bị lạnh dù kho còn than
        bool isWinter = globalSystemManager != null
            && globalSystemManager.GetGlobalDataRef().CurrentSeason == SeasonType.Winter;
        int aliveBefore = 0;
        int deaths = 0;
        for(int i = 0; i < activeCount; i++)
        {
            ref ResidentData r = ref allResidents[i];
            if(!r.isAlive)
                continue;
            aliveBefore++;
            bool coldThisMonth = (isWinter && r.assignedHouseID < 0)
                || (coldRate > 0f && UnityEngine.Random.value < coldRate);
            if (coldThisMonth)
            {
                if(r.coldMonths < byte.MaxValue)
                    r.coldMonths++;
                r.happiness = Mathf.Max(0f, r.happiness - coldHappinessPenalty);
            }
            else r.coldMonths = 0;
            if(r.coldMonths >= ColdMonthsBeforeDeathRisk && UnityEngine.Random.value < freezeDeathChance)
            {
                r.isAlive = false;
                r.coldMonths = 0;
                deaths++;
            }
        }
        ApplyMacroDeaths(deaths, aliveBefore, DeathCause.Cold);
        RefreshVisualPopulation();
    }

    public void ProcessMonthlyDisease(float diseasePressure)
    {
        if (buildingManager == null)
            ResolveDependencies();

        ResidentDiseaseSystem.ProcessMonthly(
            allResidents,
            activeCount,
            buildingManager != null ? buildingManager.allBuildings : null,
            buildingManager != null ? buildingManager.activeCount : 0,
            diseasePressure,
            diseaseSpreadFactor,
            naturalRecoveryChance,
            globalSystemManager);

        RefreshVisualPopulation();
    }

    public void ProcessMonthlyAssignment(int year, byte month, SeasonType season)
    {
        if (buildingManager == null)
            ResolveDependencies();

        if (buildingManager == null)
            return;

        buildingManager.ProcessMonthlyConstruction();
        buildingManager.ProcessMonthlyCrops(season);

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

        // Tính lại thưởng sản xuất của sắc lệnh theo tình hình tháng này trước khi sản xuất
        // (EdictManager cũng nghe MonthChanged nhưng thứ tự listener không cố định)
        if (edictManager != null)
            edictManager.EvaluateCityRules(false);

        ResidentProductionSystem.ProcessMonthly(
            allResidents,
            activeCount,
            buildingManager.allBuildings,
            buildingManager.activeCount,
            season,
            globalSystemManager,
            edictManager,
            GamePlayController.Instance != null && GamePlayController.Instance.playerContain != null
                ? GamePlayController.Instance.playerContain.researchManager
                : null);

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

        RefreshVisualPopulation();
    }

    private void RefreshVisualPopulation()
    {
        SyncAllVisualAgents();
        CompactDeadResidents();
        FillMissingVisualAgents();
        RefreshMetricsCache();
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
        // Mẫu là tập con của dân thật: dân số thật luôn >= số mẫu còn sống (không để làm tròn kéo lệch),
        // và hết mẫu = tuyệt chủng (trước đây dân số về 0 lại "hồi sinh" bằng số mẫu).
        int pop = alive == 0 ? 0 : Mathf.Max(global.totalPopulation, alive);
        int scaledInfected = alive > 0
            ? Mathf.RoundToInt(pop * ((float)infected / alive))
            : 0;

        // Hạnh phúc từng người 0–1; chỉ số toàn thành lưu theo % (0–100) cho HUD và sắc lệnh
        globalSystemManager.UpdateMetricsCache(pop, scaledInfected, avgHappiness * 100f);
        GameEvents.Post(EventID.PopulationChanged, pop);
    }

    public void RebindAllVisualAgents()
    {
        CompactDeadResidents();
        ClearAllVisualAgents();

        if (residentPrefab == null)
        {
            Debug.LogWarning("[ResidentManager] Chưa gán residentPrefab — bỏ qua visual rebind.");
            return;
        }

        ResolveDependencies();
        FillMissingVisualAgents();
        Debug.Log($"[ResidentManager] Đã bind {_activeVisuals.Count}/{activeCount} visual agent(s).");
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

            if (!view.IsBound || (uint)view.DataIndex >= (uint)activeCount)
            {
                view.Unbind();
                SimplePool2.Despawn(view.gameObject);
                _activeVisuals.RemoveAt(i);
                continue;
            }

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

        for (int priority = 3; priority >= 0 && _activeVisuals.Count < maxVisualAgents; priority--)
        {
            for (int i = 0; i < activeCount && _activeVisuals.Count < maxVisualAgents; i++)
            {
                if (_visualBoundFlags[i] || !allResidents[i].isAlive)
                    continue;
                if (VisualFillPriority(in allResidents[i]) != priority)
                    continue;

                if (TrySpawnVisual(i, in allResidents[i]))
                    _visualBoundFlags[i] = true;
            }
        }
    }

    private static int VisualFillPriority(in ResidentData resident)
    {
        if (resident.healthStatus == HealthStatus.ActiveInfected)
            return 3;
        if (resident.assignedWorkID >= 0)
            return 2;
        if (resident.assignedHouseID >= 0)
            return 1;
        return 0;
    }

    private void CompactDeadResidents()
    {
        int write = 0;
        for (int read = 0; read < activeCount; read++)
        {
            if (!allResidents[read].isAlive)
                continue;

            if (write != read)
            {
                allResidents[write] = allResidents[read];
                RemapVisualDataIndex(read, write);
            }

            write++;
        }

        activeCount = write;
    }

    private void RemapVisualDataIndex(int from, int to)
    {
        for (int i = 0; i < _activeVisuals.Count; i++)
        {
            ResidentBase view = _activeVisuals[i];
            if (view != null && view.DataIndex == from)
                view.RebindIndex(to);
        }
    }

    private bool TrySpawnVisual(int index, in ResidentData resident)
    {
        if (residentPrefab == null || !resident.isAlive || _activeVisuals.Count >= maxVisualAgents)
            return false;

        _goalHour = CurrentHour();
        Vector3 worldPosition = ResolveCommuteGoal(in resident).world;
        ResidentBase view = SimplePool2.Spawn(residentPrefab, worldPosition, Quaternion.identity);

        if (visualRoot != null)
            view.transform.SetParent(visualRoot, true);

        view.Bind(index, in resident, worldPosition);
        ApplyDepthSorting(view);
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

    private void Update()
    {
        TickVisualCommutes();
    }

    private void TickVisualCommutes()
    {
        if (_activeVisuals.Count == 0)
            return;

        // Đi bộ nhanh theo tốc độ game (x2/x3) để lịch sinh hoạt vẫn khớp giờ trong vòng ngày–đêm
        float speedMult = globalSystemManager != null ? globalSystemManager.SpeedMultiplier : 1f;
        float step = commuteSpeed * Time.deltaTime * speedMult;
        if (step <= 0f)
            return;

        EnsurePathfinder();
        RefreshScheduleCache();
        int gridVersion = gridController != null ? gridController.OccupancyVersion : 0;
        int pathBudget = Mathf.Max(1, maxPathRequestsPerFrame);

        int count = _activeVisuals.Count;
        if (_pathRoundRobin >= count)
            _pathRoundRobin = 0;

        // Bắt đầu từ vị trí xoay vòng để mọi cư dân đều tới lượt tìm đường
        for (int n = 0; n < count; n++)
        {
            int i = (_pathRoundRobin + n) % count;
            ResidentBase view = _activeVisuals[i];
            if (view == null || !view.IsBound)
                continue;
            if ((uint)view.DataIndex >= (uint)activeCount)
                continue;

            ref ResidentData data = ref GetResidentRef(view.DataIndex);
            if (!data.isAlive)
                continue;

            CommuteGoal goal = ResolveCommuteGoal(in data);
            if (pathBudget > 0 && view.NeedsPath(goal.world, gridVersion))
            {
                pathBudget--;
                if (pathBudget == 0)
                    _pathRoundRobin = (i + 1) % count;
                AssignPath(view, in goal, gridVersion);
            }

            view.TickPath(step);
            view.SetIndoors(goal.indoors && !view.IsMoving);
            ApplyDepthSorting(view);
        }
    }

    // Cùng sorting layer + thang order với cây/building → vật phía trước che resident đúng
    private void ApplyDepthSorting(ResidentBase view)
    {
        if (gridController == null)
            return;

        view.SetDepthSorting(gridController.SortingLayerId, gridController.GetDynamicSortingOrder(view.transform.position));
    }

    private void AssignPath(ResidentBase view, in CommuteGoal goal, int gridVersion)
    {
        _worldPathBuffer.Clear();

        if (_pathfinder != null && _pathfinder.IsValid)
        {
            Vector2Int startCell = gridController.WorldToGrid(view.transform.position);
            ushort startBuilding = gridController.GetOccupantId(startCell.x, startCell.y);

            if (_pathfinder.FindPath(startCell, goal.cell, startBuilding, goal.buildingId, _cellPathBuffer))
            {
                // Bỏ ô cuối — thay bằng điểm đích chính xác (tâm công trình + slot offset)
                for (int c = 0; c < _cellPathBuffer.Count - 1; c++)
                {
                    Vector3 p = gridController.GridToWorld(_cellPathBuffer[c].x, _cellPathBuffer[c].y);
                    p.z = 0f;
                    _worldPathBuffer.Add(p);
                }
            }
        }

        _worldPathBuffer.Add(goal.world);
        view.SetPath(_worldPathBuffer, goal.world, gridVersion);
    }

    private CommuteGoal ResolveCommuteGoal(in ResidentData data)
    {
        if (_debugGoals.Count > 0 && _debugGoals.TryGetValue(data.residentID, out CommuteGoal debugGoal))
            return debugGoal;

        // Lịch sinh hoạt kiểu Cities: Skylines — 1 tháng = 1 vòng ngày–đêm (DayCycle), giờ lệch theo từng người
        float hour = _goalHour;
        int id = data.residentID;
        bool hasHouse = TryResolveBuildingGoal(data.assignedHouseID, id, 0, out CommuteGoal house, out BuildingType houseType);
        bool hasWork = TryResolveBuildingGoal(data.assignedWorkID, id, 1, out CommuteGoal work, out _);

        // 1. Bệnh nhân được xếp vào Phòng khám: nằm ở đó cả ngày
        if (hasWork && ResidentAssignmentRules.NeedsClinicCare(in data))
            return work;

        // 2. Đi làm / đi học theo ca, xuất phát sớm theo quãng đường để tới đúng giờ
        if (hasWork && IsWorkTime(in data, hour, hasHouse ? house.world : work.world, work.world))
            return work;

        if (hasHouse)
        {
            // 3. Người không đi làm (già, thất nghiệp, trẻ chưa đi học) ra quảng trường ban ngày
            if (houseType != BuildingType.QuarantineWard && WantsOuting(in data, hasWork, hour)
                && TryGetPlazaGoal(id, out CommuteGoal plaza))
                return plaza;

            // 4. Ở nhà; tới giờ ngủ thì vào trong
            house.indoors = IsSleepTime(in data, hour);
            return house;
        }

        // 5. Vô gia cư: đêm co cụm quanh lửa trại gần nhất, ngày tụ ở giữa làng
        Vector2Int fallbackCell = FallbackCell(id);
        CommuteGoal street = new CommuteGoal
        {
            world = CellToWorld(fallbackCell.x, fallbackCell.y) + SlotOffset(id, 0),
            cell = fallbackCell,
            buildingId = 0
        };
        if (DayCycle.IsBetween(hour, 19f + DayCycle.PersonalJitter(id, 0.75f, 7), 6f)
            && TryGetNearestCampfire(street.world, id, out CommuteGoal camp))
            return camp;
        return street;
    }

    /// <summary>Giờ trong vòng ngày–đêm dùng cho mọi cư dân trong frame này.</summary>
    private float CurrentHour() =>
        globalSystemManager != null ? DayCycle.GetHour(in globalSystemManager.GetGlobalDataRef()) : 12f;

    private bool IsNightShift(in ResidentData data) =>
        (data.professionType == ProfessionType.Guard || data.professionType == ProfessionType.Doctor)
        && DayCycle.PersonalHash(data.residentID, 11) % nightShiftOneIn == 0;

    private bool IsWorkTime(in ResidentData data, float hour, Vector3 from, Vector3 to)
    {
        float start, end;
        if (data.professionType == ProfessionType.Student)
        {
            start = DayCycle.SchoolStart;
            end = DayCycle.SchoolEnd;
        }
        else if (IsNightShift(in data))
        {
            start = DayCycle.NightShiftStart;
            end = DayCycle.NightShiftEnd;
        }
        else
        {
            start = DayCycle.WorkStart;
            end = DayCycle.WorkEnd;
        }

        // Giờ đi đường (giờ trong vòng): quãng đường thẳng × hệ số đường vòng / tốc độ đi bộ
        float secondsPerHour = globalSystemManager != null ? globalSystemManager.RealSecondsPerMonth / 24f : 5f;
        float travelHours = commuteSpeed > 0f && secondsPerHour > 0f
            ? Vector3.Distance(from, to) * 1.4f / (commuteSpeed * secondsPerHour)
            : 0f;
        travelHours = Mathf.Min(travelHours, 3f);

        int id = data.residentID;
        float leave = start - travelHours + DayCycle.PersonalJitter(id, 0.5f, 1);
        float finish = end + DayCycle.PersonalJitter(id, 0.6f, 2);
        return DayCycle.IsBetween(hour, leave, finish);
    }

    private bool WantsOuting(in ResidentData data, bool hasWork, float hour)
    {
        if (hasWork || _plazaBuildingId == 0)
            return false;
        if (data.healthStatus == HealthStatus.ActiveInfected || data.healthStatus == HealthStatus.Treated)
            return false;
        if (data.GetAgeGroup() == AgeGroupType.Child && data.age < ResidentAssignmentRules.SchoolStartAge)
            return false;
        if (DayCycle.PersonalHash(data.residentID, 23) % 3 >= outingThirds)
            return false;

        int id = data.residentID;
        return DayCycle.IsBetween(hour,
            DayCycle.OutingStart + DayCycle.PersonalJitter(id, 1f, 3),
            DayCycle.OutingEnd + DayCycle.PersonalJitter(id, 1f, 4));
    }

    private bool IsSleepTime(in ResidentData data, float hour)
    {
        int id = data.residentID;
        // Ca đêm ngủ ban ngày
        if (data.assignedWorkID >= 0 && IsNightShift(in data))
            return DayCycle.IsBetween(hour, 8f + DayCycle.PersonalJitter(id, 0.5f, 5), 16f);

        return DayCycle.IsBetween(hour,
            DayCycle.BedTime + DayCycle.PersonalJitter(id, 1f, 5),
            DayCycle.WakeTime + DayCycle.PersonalJitter(id, 0.5f, 6));
    }

    /// <summary>Quét công trình 1 lần mỗi frame: quảng trường (Tòa thị chính) và các lửa trại đang cháy.</summary>
    private void RefreshScheduleCache()
    {
        _goalHour = CurrentHour();
        _plazaBuildingId = 0;
        _campfires.Clear();
        if (buildingManager == null)
            return;

        for (int i = 0; i < buildingManager.activeCount; i++)
        {
            ref BuildingData b = ref buildingManager.allBuildings[i];
            if (!BuildingEffectRules.IsEffectActive(in b))
                continue;

            if (b.buildingType == BuildingType.CityHall && _plazaBuildingId == 0)
            {
                Vector2Int size = BuildingRules.GetFootprint(in b);
                _plazaBuildingId = b.buildingID;
                _plazaWorld = GetBuildingWorldPosition(in b);
                _plazaCell = new Vector2Int(b.coordX + (size.x - 1) / 2, b.coordY + (size.y - 1) / 2);
            }
            else if (b.buildingType == BuildingType.Campfire)
            {
                _campfires.Add(new CommuteGoal
                {
                    world = GetBuildingWorldPosition(in b),
                    cell = new Vector2Int(b.coordX, b.coordY),
                    buildingId = b.buildingID
                });
            }
        }
    }

    private bool TryGetPlazaGoal(int residentId, out CommuteGoal goal)
    {
        goal = default;
        if (_plazaBuildingId == 0)
            return false;

        goal.world = _plazaWorld + SlotOffset(residentId, 2) * 2.5f;
        goal.cell = _plazaCell;
        goal.buildingId = _plazaBuildingId;
        return true;
    }

    private bool TryGetNearestCampfire(Vector3 near, int residentId, out CommuteGoal goal)
    {
        goal = default;
        float best = float.MaxValue;
        for (int i = 0; i < _campfires.Count; i++)
        {
            float d = (_campfires[i].world - near).sqrMagnitude;
            if (d < best)
            {
                best = d;
                goal = _campfires[i];
            }
        }

        if (best == float.MaxValue)
            return false;
        goal.world += SlotOffset(residentId, 3) * 1.5f;
        return true;
    }

    private bool TryResolveBuildingGoal(int buildingId, int residentId, int slot, out CommuteGoal goal, out BuildingType type)
    {
        goal = default;
        type = BuildingType.None;
        if (buildingManager == null || buildingId < 0)
            return false;

        int index = buildingManager.FindBuildingIndexById((ushort)buildingId);
        if (index < 0)
            return false;

        ref BuildingData building = ref buildingManager.GetBuildingRef(index);
        type = building.buildingType;
        Vector2Int footprint = Vector2Int.Max(Vector2Int.one, BuildingRules.GetFootprint(in building));
        goal.world = GetBuildingWorldPosition(in building) + SlotOffset(residentId, slot);
        goal.cell = new Vector2Int(
            building.coordX + (footprint.x - 1) / 2,
            building.coordY + (footprint.y - 1) / 2);
        goal.buildingId = building.buildingID;
        return true;
    }

    private static Vector3 SlotOffset(int residentId, int slot)
    {
        int seed = residentId * 3 + slot;
        float x = (seed % 7 - 3) * 0.18f;
        float y = ((seed / 7) % 5 - 2) * 0.12f;
        return new Vector3(x, y, 0f);
    }

    // Dân chưa có nhà tụ tập quanh giữa bản đồ (nơi camera mở ra), rải trong vùng 3x3 ô
    private Vector2Int FallbackCell(int residentId)
    {
        Vector2Int center = gridController != null
            ? new Vector2Int(gridController.Width / 2, gridController.Height / 2)
            : new Vector2Int(10, 10);

        return new Vector2Int(center.x - 1 + residentId % 3, center.y - 1 + (residentId / 3) % 3);
    }
}
