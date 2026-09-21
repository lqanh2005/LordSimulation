using UnityEngine;
using UnityEngine.InputSystem;

public class PlacementResidentVisualTest : MonoBehaviour
{
    [SerializeField] private Grid isometricGrid;
    [SerializeField] private BuildingManager buildingManager;
    [SerializeField] private GlobalSystemManager globalSystemManager;
    [SerializeField] private PlacementManager placementManager;
    [SerializeField] private ResidentBase residentPrefab;
    [SerializeField] private Sprite residentSprite;
    [SerializeField] private int residentsPerHouse = 2;
    [SerializeField] private float visualMonthSeconds = 6f;
    [SerializeField] private Vector3Int seedHouseCell = Vector3Int.zero;
    [SerializeField] private Vector3Int seedFarmCell = new Vector3Int(4, 0, 0);
    [SerializeField] private bool autoSeedDemo = true;

    private ResidentManager _residentManager;
    private Transform _visualRoot;
    private int _processedBuildings;
    private string _status = "Chưa chạy test";
    private bool _testPassed;

    private void Awake()
    {
        if (isometricGrid == null)
            isometricGrid = FindFirstObjectByType<Grid>();
        if (buildingManager == null)
            buildingManager = FindFirstObjectByType<BuildingManager>();
        if (globalSystemManager == null)
            globalSystemManager = FindFirstObjectByType<GlobalSystemManager>();
        if (placementManager == null)
            placementManager = FindFirstObjectByType<PlacementManager>();

        _visualRoot = new GameObject("ResidentVisualRoot").transform;
        _visualRoot.SetParent(transform, false);

        ResidentBase prefab = residentPrefab != null ? residentPrefab : CreateResidentPrefab();
        _residentManager = gameObject.AddComponent<ResidentManager>();
        _residentManager.ConfigureVisualWorld(
            isometricGrid,
            _visualRoot,
            prefab,
            buildingManager,
            globalSystemManager,
            3.5f);
        _residentManager.Init();

        if (globalSystemManager != null)
        {
            ref GlobalSystemData data = ref globalSystemManager.GetGlobalDataRef();
            data.timeTickProgress = 0.55f;
        }
    }

    private void Start()
    {
        _testPassed = RunGridAlignmentTest();
        if (autoSeedDemo)
            SeedDemoBuildings();
        SyncNewBuildings();
        if (_testPassed)
            _testPassed = VerifyResidentsOnHouseGrid();
        _status = _testPassed
            ? "PASS — dân đứng đúng ô Grid isometric"
            : "FAIL — xem Console";
        Debug.Log($"[ResidentVisualTest] {_status}");
    }

    private void Update()
    {
        SyncNewBuildings();
        CycleCommuteClock();
        HandleHotkeys();
    }

    private void OnGUI()
    {
        GUI.color = _testPassed ? Color.green : Color.red;
        GUI.Label(new Rect(16f, 12f, 720f, 28f), _status);
        GUI.color = Color.white;
        GUI.Label(new Rect(16f, 40f, 720f, 60f),
            "Click: đặt công trình  |  1: Nhà  |  3: Farm  |  6: Furnace\nDân đi nhà <-> nơi làm theo nhịp tháng.");
    }

    private bool RunGridAlignmentTest()
    {
        if (isometricGrid == null || _residentManager == null)
        {
                Debug.LogError("[ResidentVisualTest] Thiếu Grid hoặc ResidentManager.");
            return false;
        }

        bool pass = true;
        pass &= AssertCell(0, 0, 1);
        pass &= AssertCell(3, 1, 1);
        pass &= AssertCell(4, 0, 2);
        return pass;
    }

    private bool AssertCell(int x, int y, int size)
    {
        Vector3Int origin = new Vector3Int(x, y, 0);
        Vector3Int top = origin + new Vector3Int(size - 1, size - 1, 0);
        Vector3 expected = (isometricGrid.GetCellCenterWorld(origin) + isometricGrid.GetCellCenterWorld(top)) * 0.5f;
        expected.z = 0f;
        Vector3 actual = _residentManager.CellToWorld(x, y, size);
        float dist = Vector3.Distance(expected, actual);
        bool ok = dist < 0.001f;
        if (ok)
            Debug.Log($"[ResidentVisualTest] PASS cell ({x},{y}) size {size} -> {actual}");
        else
            Debug.LogError($"[ResidentVisualTest] FAIL cell ({x},{y}) size {size}. expected {expected} actual {actual} d={dist}");
        return ok;
    }

    private void SeedDemoBuildings()
    {
        if (placementManager == null)
            return;

        placementManager.PlaceAt(seedHouseCell, BuildingType.House);
        placementManager.PlaceAt(seedFarmCell, BuildingType.Farm);
    }

    private void SyncNewBuildings()
    {
        if (buildingManager == null)
            return;

        while (_processedBuildings < buildingManager.activeCount)
        {
            OnBuildingAdded(_processedBuildings);
            _processedBuildings++;
        }
    }

    private void OnBuildingAdded(int index)
    {
        ref BuildingData building = ref buildingManager.GetBuildingRef(index);
        if (ResidentAssignmentRules.IsHousingBuilding(building.buildingType))
            SpawnResidentsForHouse(in building);

        AssignWorkplace(in building);
    }

    private void SpawnResidentsForHouse(in BuildingData house)
    {
        int nextId = _residentManager.PeekMaxResidentId() + 1;
        int count = Mathf.Max(1, residentsPerHouse);
        ushort workId = FindWorkplaceId();

        for (int i = 0; i < count; i++)
        {
            ResidentData resident = ImmigrationRules.CreateApplicant(nextId++);
            resident.assignedHouseID = (short)house.buildingID;
            resident.assignedWorkID = workId == 0
                ? ResidentAssignmentRules.UnassignedId
                : (short)workId;
            if (workId != 0)
            {
                ProfessionType job = ResolveJobForWorkId(workId);
                if (job != ProfessionType.None)
                    resident.professionType = job;
            }

            _residentManager.AddResident(in resident);
        }
    }

    private void AssignWorkplace(in BuildingData building)
    {
        if (!ResidentAssignmentRules.IsWorkplaceBuilding(building.buildingType))
            return;

        ProfessionType job = ResidentAssignmentRules.GetProfessionForWorkplace(building.buildingType);
        for (int i = 0; i < _residentManager.activeCount; i++)
        {
            ref ResidentData resident = ref _residentManager.GetResidentRef(i);
            if (!resident.isAlive || resident.assignedWorkID >= 0)
                continue;

            resident.assignedWorkID = (short)building.buildingID;
            if (job != ProfessionType.None)
                resident.professionType = job;
        }
    }

    private ushort FindWorkplaceId()
    {
        for (int i = 0; i < buildingManager.activeCount; i++)
        {
            ref BuildingData building = ref buildingManager.GetBuildingRef(i);
            if (ResidentAssignmentRules.IsWorkplaceBuilding(building.buildingType))
                return building.buildingID;
        }

        return 0;
    }

    private ProfessionType ResolveJobForWorkId(ushort workId)
    {
        int index = buildingManager.FindBuildingIndexById(workId);
        if (index < 0)
            return ProfessionType.None;

        return ResidentAssignmentRules.GetProfessionForWorkplace(
            buildingManager.GetBuildingRef(index).buildingType);
    }

    private bool VerifyResidentsOnHouseGrid()
    {
        if (_visualRoot == null || buildingManager == null || buildingManager.activeCount <= 0)
        {
            Debug.LogError("[ResidentVisualTest] Không có nhà/dân để đối chiếu.");
            return false;
        }

        int houseIndex = -1;
        for (int i = 0; i < buildingManager.activeCount; i++)
        {
            if (ResidentAssignmentRules.IsHousingBuilding(buildingManager.GetBuildingRef(i).buildingType))
            {
                houseIndex = i;
                break;
            }
        }

        if (houseIndex < 0)
            return false;

        ref BuildingData house = ref buildingManager.GetBuildingRef(houseIndex);
        Vector3 houseWorld = _residentManager.GetBuildingWorldPosition(in house);
        ResidentBase[] views = _visualRoot.GetComponentsInChildren<ResidentBase>();
        if (views == null || views.Length == 0)
        {
            Debug.LogError("[ResidentVisualTest] Không spawn được visual dân.");
            return false;
        }

        bool anyNearHouse = false;
        for (int i = 0; i < views.Length; i++)
        {
            if (views[i] == null || !views[i].IsBound)
                continue;
            if (Vector3.Distance(views[i].transform.position, houseWorld) < 0.9f)
                anyNearHouse = true;
        }

        if (!anyNearHouse)
            Debug.LogError($"[ResidentVisualTest] Visual dân lệch khỏi nhà. houseWorld={houseWorld}");
        else
            Debug.Log($"[ResidentVisualTest] PASS {views.Length} dan dung gan nha {houseWorld}");

        return anyNearHouse;
    }

    private void CycleCommuteClock()
    {
        if (globalSystemManager == null || visualMonthSeconds <= 0.01f)
            return;

        ref GlobalSystemData data = ref globalSystemManager.GetGlobalDataRef();
        data.timeTickProgress += Time.deltaTime / visualMonthSeconds;
        if (data.timeTickProgress >= 1f)
            data.timeTickProgress -= 1f;
    }

    private void HandleHotkeys()
    {
        if (placementManager == null || Keyboard.current == null)
            return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
            placementManager.currentSelectedType = BuildingType.House;
        else if (Keyboard.current.digit3Key.wasPressedThisFrame)
            placementManager.currentSelectedType = BuildingType.Farm;
        else if (Keyboard.current.digit6Key.wasPressedThisFrame)
            placementManager.currentSelectedType = BuildingType.Furnace;
    }

    private ResidentBase CreateResidentPrefab()
    {
        GameObject go = new GameObject("ResidentVisualPrefab");
        go.transform.SetParent(transform, false);
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = residentSprite;
        renderer.color = Color.white;
        renderer.sortingOrder = 15;
        renderer.sortingLayerID = 1766568969;
        renderer.spriteSortPoint = SpriteSortPoint.Pivot;
        ResidentBase view = go.AddComponent<ResidentBase>();
        go.SetActive(false);
        return view;
    }
}
