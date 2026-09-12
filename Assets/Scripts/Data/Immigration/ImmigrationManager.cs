using UnityEngine;

public class ImmigrationManager : MonoBehaviour
{
    public const int MAX_WAITING = ImmigrationRules.MaxWaiting;

    [Header("Hang cho cong thanh")]
    public ResidentData[] waitingApplicants = new ResidentData[MAX_WAITING];
    public int waitingCount = 0;

    [SerializeField] private ResidentManager residentManager;
    [SerializeField] private BuildingManager buildingManager;
    [SerializeField] private GlobalSystemManager globalSystemManager;
    [SerializeField] private EdictManager edictManager;
    [SerializeField] private ImmigrationPopupUI popupUi;
    public ImmigrationGate gate;
    private bool _suppressArrivals;
    private int _lastArrivalYear = -1;
    private byte _lastArrivalMonth;

    public int InspectIndex { get; private set; }

    public bool HasApplicants => waitingCount > 0;

    public void Init()
    {
        ResolveDependencies();
        if (popupUi == null)
            popupUi = GetComponent<ImmigrationPopupUI>() ?? gameObject.AddComponent<ImmigrationPopupUI>();
        popupUi.Bind(this);
        EnsureGate();
        gate.Bind(this);
        if (waitingCount == 0)
            EnqueueMonthlyArrivals(SeasonType.Spring);
        else
            gate.SetWaitingCount(waitingCount);
    }

    private void OnEnable()
    {
        ResolveDependencies();
        GameEvents.Unlisten(EventID.MonthChanged, OnMonthChanged);
        GameEvents.Listen(EventID.MonthChanged, OnMonthChanged);
    }

    private void OnDisable()
    {
        GameEvents.Unlisten(EventID.MonthChanged, OnMonthChanged);
    }

    private void OnMonthChanged(object param)
    {
        if (_suppressArrivals)
            return;

        MonthChangedPayload payload = (MonthChangedPayload)param;
        if (payload.year == _lastArrivalYear && payload.month == _lastArrivalMonth)
            return;

        _lastArrivalYear = payload.year;
        _lastArrivalMonth = payload.month;
        ProcessQuarantineUpkeep();
        EnqueueMonthlyArrivals(payload.season);
    }

    private void ResolveDependencies()
    {
        if (GamePlayController.Instance == null || GamePlayController.Instance.playerContain == null)
            return;

        PlayerContain contain = GamePlayController.Instance.playerContain;
        if (residentManager == null)
            residentManager = contain.residentManager;
        if (buildingManager == null)
            buildingManager = contain.buildingManager;
        if (globalSystemManager == null)
            globalSystemManager = contain.globalSystemManager;
        if (edictManager == null)
            edictManager = contain.edictManager;
        if (contain.immigrationManager == null)
            contain.immigrationManager = this;
    }

    private void EnsureGate()
    {
        if (gate != null)
            return;

        gate = FindObjectOfType<ImmigrationGate>();
        if (gate != null)
            return;

        GameObject go = new GameObject("CityGate");
        go.transform.SetParent(transform, false);
        go.transform.position = new Vector3(-4f, 0f, 0f);
        go.transform.localScale = new Vector3(3.5f, 5.5f, 1f);
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.color = new Color(0.75f, 0.55f, 0.25f);
        renderer.sprite = CreateGateSprite();
        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(1.4f, 2.2f);
        gate = go.AddComponent<ImmigrationGate>();
    }

    private static Sprite CreateGateSprite()
    {
        Texture2D tex = Texture2D.whiteTexture;
        return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 8f);
    }

    public void EnqueueMonthlyArrivals(SeasonType season)
    {
        float reputation = 0f;
        if (globalSystemManager != null)
            reputation = globalSystemManager.GetGlobalDataRef().reputation;
        int arrivals = ImmigrationRules.RollArrivalCount(season, reputation);
        int added = 0;
        int nextId = NextApplicantId();
        for (int i = 0; i < arrivals && waitingCount < MAX_WAITING; i++)
        {
            waitingApplicants[waitingCount] = ImmigrationRules.CreateApplicant(nextId++);
            waitingCount++;
            added++;
        }

        if (added <= 0)
            return;

        InspectIndex = Mathf.Clamp(InspectIndex, 0, waitingCount - 1);
        NotifyQueueChanged();
    }

    public void SetSuppressArrivals(bool suppress)
    {
        _suppressArrivals = suppress;
    }

    public void RefreshAfterLoad()
    {
        if (globalSystemManager != null)
        {
            ref GlobalSystemData global = ref globalSystemManager.GetGlobalDataRef();
            _lastArrivalYear = global.currentYear;
            _lastArrivalMonth = global.currentMonth;
        }

        InspectIndex = 0;
        if (gate == null)
            EnsureGate();
        NotifyQueueChanged();
    }

    public void OpenInspection()
    {
        if (waitingCount <= 0)
        {
            if (popupUi != null)
                popupUi.ShowEmpty();
            return;
        }

        InspectIndex = Mathf.Clamp(InspectIndex, 0, waitingCount - 1);
        ShowCurrent();
    }

    public bool TryGetCurrent(out ResidentData applicant)
    {
        if (waitingCount <= 0 || (uint)InspectIndex >= (uint)waitingCount)
        {
            applicant = default;
            return false;
        }

        applicant = waitingApplicants[InspectIndex];
        return true;
    }

    public RuleAction GetSuggestedAction(in ResidentData applicant)
    {
        if (edictManager == null || globalSystemManager == null)
            return RuleAction.Admit;

        if (EdictRules.TryGetSuggestedAction(
                edictManager.allEdicts,
                edictManager.activeCount,
                in applicant,
                in globalSystemManager.GetGlobalDataRef(),
                out RuleAction action,
                out _))
            return action;

        return RuleAction.Admit;
    }

    public void AcceptCurrent()
    {
        if (!TryGetCurrent(out ResidentData applicant))
            return;

        CollectEntryTax(ref applicant, out int tax);
        if (!Admit(in applicant, false))
            return;

        if (tax > 0 && globalSystemManager != null)
            globalSystemManager.ModifyGold(tax);

        RemoveCurrent();
    }

    public void DenyCurrent()
    {
        if (!TryGetCurrent(out ResidentData applicant))
            return;

        if (globalSystemManager != null)
            globalSystemManager.ModifyReputation(ImmigrationRules.GetDenyReputationGain(in applicant));

        RemoveCurrent();
    }

    public void QuarantineCurrent()
    {
        if (!TryGetCurrent(out ResidentData applicant))
            return;

        CollectEntryTax(ref applicant, out int tax);
        if (!Admit(in applicant, true))
            return;

        if (tax > 0 && globalSystemManager != null)
            globalSystemManager.ModifyGold(tax);
        ConsumeQuarantineSupplies(1);
        RemoveCurrent();
    }

    private bool Admit(in ResidentData applicant, bool quarantine)
    {
        if (residentManager == null)
            ResolveDependencies();
        if (residentManager == null)
            return false;

        ResidentData admitted = applicant;
        int index = residentManager.AddResident(in admitted);
        if (index < 0)
            return false;

        if (quarantine)
            TryAssignQuarantine(index);

        if (globalSystemManager != null)
        {
            ref GlobalSystemData global = ref globalSystemManager.GetGlobalDataRef();
            global.totalPopulation++;
        }

        residentManager.NotifyPopulationChanged();
        return true;
    }

    private void CollectEntryTax(ref ResidentData applicant, out int tax)
    {
        tax = ImmigrationRules.GetEntryTax(in applicant, ResolveTariffPercent(in applicant));
        if (tax > 0)
            ImmigrationRules.PayTax(ref applicant, tax);
    }

    private float ResolveTariffPercent(in ResidentData applicant)
    {
        if (edictManager == null || globalSystemManager == null)
            return ImmigrationRules.DefaultTariffPercent;

        if (!EdictRules.TryGetSuggestedAction(
                edictManager.allEdicts,
                edictManager.activeCount,
                in applicant,
                in globalSystemManager.GetGlobalDataRef(),
                out _,
                out int edictIndex))
            return ImmigrationRules.DefaultTariffPercent;

        if (edictIndex < 0)
            return ImmigrationRules.DefaultTariffPercent;

        float tariff = edictManager.GetEdictRef(edictIndex).tariffPercent;
        return tariff > 0f ? tariff : ImmigrationRules.DefaultTariffPercent;
    }

    private void ConsumeQuarantineSupplies(int people)
    {
        if (people <= 0 || globalSystemManager == null)
            return;

        globalSystemManager.ConsumeAvailable(
            ResourceType.Food, people * ImmigrationRules.QuarantineFoodPerMonth);
        globalSystemManager.ConsumeAvailable(
            ResourceType.Medicine, people * ImmigrationRules.QuarantineMedicinePerMonth);
    }

    private void ProcessQuarantineUpkeep()
    {
        if (residentManager == null || buildingManager == null)
            ResolveDependencies();
        if (residentManager == null || buildingManager == null || globalSystemManager == null)
            return;

        int quarantined = 0;
        for (int i = 0; i < residentManager.activeCount; i++)
        {
            ref ResidentData r = ref residentManager.GetResidentRef(i);
            if (!r.isAlive)
                continue;
            if (ResidentAssignmentRules.IsHousedInQuarantine(
                    in r, buildingManager.allBuildings, buildingManager.activeCount))
                quarantined++;
        }

        ConsumeQuarantineSupplies(quarantined);
    }

    private void TryAssignQuarantine(int residentIndex)
    {
        if (buildingManager == null)
            return;

        ref ResidentData resident = ref residentManager.GetResidentRef(residentIndex);
        int best = -1;
        for (int i = 0; i < buildingManager.activeCount; i++)
        {
            ref BuildingData building = ref buildingManager.GetBuildingRef(i);
            if (building.buildingType != BuildingType.QuarantineWard)
                continue;
            if (!ResidentAssignmentRules.HasHousingSlot(in building))
                continue;
            best = i;
            break;
        }

        if (best < 0)
            return;

        ref BuildingData ward = ref buildingManager.GetBuildingRef(best);
        ResidentAssignmentRules.AssignHousing(ref resident, ward.buildingID);
        ward.currentOccupancy++;
    }

    private void RemoveCurrent()
    {
        if (waitingCount <= 0)
            return;

        int last = waitingCount - 1;
        if (InspectIndex != last)
            waitingApplicants[InspectIndex] = waitingApplicants[last];
        waitingCount--;
        if (InspectIndex >= waitingCount)
            InspectIndex = waitingCount - 1;
        if (InspectIndex < 0)
            InspectIndex = 0;

        NotifyQueueChanged();
        if (waitingCount > 0)
            ShowCurrent();
        else if (popupUi != null)
            popupUi.ShowEmpty();
    }

    private void ShowCurrent()
    {
        if (popupUi == null || !TryGetCurrent(out ResidentData applicant))
            return;

        popupUi.ShowApplicant(in applicant, GetSuggestedAction(in applicant), waitingCount);
    }

    private void NotifyQueueChanged()
    {
        if (gate != null)
            gate.SetWaitingCount(waitingCount);
        GameEvents.Post(EventID.ImmigrationQueueChanged, waitingCount);
    }

    private int NextApplicantId()
    {
        int maxId = 0;
        if (residentManager != null)
            maxId = residentManager.PeekMaxResidentId();
        for (int i = 0; i < waitingCount; i++)
        {
            if (waitingApplicants[i].residentID > maxId)
                maxId = waitingApplicants[i].residentID;
        }

        return maxId + 1;
    }
}
